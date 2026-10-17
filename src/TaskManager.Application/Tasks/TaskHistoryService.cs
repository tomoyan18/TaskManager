using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Common;
using TaskManager.Application.Security;

namespace TaskManager.Application.Tasks;

/// <summary>タスクの変更履歴（REQ-TSK-07）。新しい順。</summary>
public sealed class TaskHistoryService(IAppDbContext db, ProjectAccessGuard guard)
{
    public async Task<PagedResponse<TaskHistoryEntryResponse>> ListAsync(
        string projectKey, int taskNumber, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.ViewProject, cancellationToken);
        var taskId = await db.TaskItems.AsNoTracking()
            .Where(t => t.ProjectId == access.ProjectId && t.Number == taskNumber).Select(t => (Guid?)t.Id)
            .SingleOrDefaultAsync(cancellationToken) ?? throw Errors.NotFound();
        var paging = PageRequest.Of(page, pageSize, 50);
        var query = db.TaskHistoryEntries.AsNoTracking().Where(h => h.TaskId == taskId);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(h => h.OccurredAtUtc).ThenByDescending(h => h.Id)
            .Skip(paging.Skip).Take(paging.PageSize).ToListAsync(cancellationToken);
        var actorIds = rows.Where(r => r.ActorUserId != null).Select(r => r.ActorUserId!.Value).Distinct().ToList();
        var actors = await db.UserAccounts.AsNoTracking().Where(u => actorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new UserRef(u.Id, u.DisplayName, u.IsActive), cancellationToken);
        var items = rows.Select(r => new TaskHistoryEntryResponse(
            r.Id, r.Kind, r.ActorUserId is { } a ? actors.GetValueOrDefault(a) : null, r.OldValue, r.NewValue, r.OccurredAtUtc)).ToList();
        return PagedResponse<TaskHistoryEntryResponse>.Create(items, paging, total);
    }
}
