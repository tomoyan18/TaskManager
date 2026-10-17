using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Common;
using TaskManager.Domain.Common;

namespace TaskManager.Application.Tasks;

/// <summary>タスク検索（REQ-SRC-01）。キー（SALES-12）の完全一致を先頭に、タイトルの部分一致を続ける。</summary>
public sealed class TaskSearchService(IAppDbContext db, ICurrentUser currentUser)
{
    public const int MaxResults = 20;

    public async Task<IReadOnlyList<TaskSearchResultResponse>> SearchAsync(string q, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        var term = q.Trim();
        var projects = await db.Projects.AsNoTracking()
            .Where(p => p.ArchivedAtUtc == null && db.ProjectMemberships.Any(m => m.ProjectId == p.Id && m.UserId == userId))
            .Select(p => new { p.Id, p.Key, p.Name })
            .ToListAsync(cancellationToken);
        var projectIds = projects.Select(p => p.Id).ToList();
        var rows = new List<Domain.Tasks.TaskItem>();

        if (TaskKey.TryParse(term, out var taskKey))
        {
            var project = projects.SingleOrDefault(p => p.Key == taskKey.ProjectKey.Value);
            if (project is not null)
            {
                var exact = await db.TaskItems.AsNoTracking()
                    .SingleOrDefaultAsync(t => t.ProjectId == project.Id && t.Number == taskKey.Number, cancellationToken);
                if (exact is not null)
                {
                    rows.Add(exact);
                }
            }
        }

        var excluded = rows.Select(r => r.Id).ToList();
        rows.AddRange(await db.TaskItems.AsNoTracking()
            .Where(t => projectIds.Contains(t.ProjectId) && t.Title.Contains(term) && !excluded.Contains(t.Id))
            .OrderByDescending(t => t.UpdatedAtUtc)
            .Take(MaxResults - rows.Count)
            .ToListAsync(cancellationToken));

        var statusIds = rows.Select(r => r.StatusId).Distinct().ToList();
        var statuses = await db.WorkflowStatuses.AsNoTracking().Where(s => statusIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => new StatusRef(s.Id, s.Name, s.Category, s.Color), cancellationToken);
        var userIds = rows.Where(r => r.AssigneeUserId != null).Select(r => r.AssigneeUserId!.Value).Distinct().ToList();
        var users = await db.UserAccounts.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new UserRef(u.Id, u.DisplayName, u.IsActive), cancellationToken);
        var projectMap = projects.ToDictionary(p => p.Id);
        return rows.Select(t =>
        {
            var p = projectMap[t.ProjectId];
            return new TaskSearchResultResponse(
                TaskKey.Format(p.Key, t.Number), t.Title, new ProjectRef(p.Key, p.Name), statuses[t.StatusId],
                t.AssigneeUserId is { } a ? users.GetValueOrDefault(a) : null);
        }).ToList();
    }
}
