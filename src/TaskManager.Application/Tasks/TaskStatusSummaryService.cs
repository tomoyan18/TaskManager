using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Common;
using TaskManager.Application.Security;
using TaskManager.Domain.Common;

namespace TaskManager.Application.Tasks;

/// <summary>
/// ステータス別の件数・割合（REQ-CHT-01、REQ-CHT-02）。件数は DB で集計し（GROUP BY）、タスクを全件読み込まない。
/// 割合は Domain の ShareRounding（最大剰余法で合計 100.0%）で求める。
/// </summary>
public sealed class TaskStatusSummaryService(IAppDbContext db, ICurrentUser currentUser, ProjectAccessGuard guard)
{
    /// <summary>区分ごとの集計で使う表示色（既定のステータスと同じ。domain-model.md §5.2 SR-05）。</summary>
    private static readonly IReadOnlyList<(StatusCategory Category, StatusColor Color)> Categories =
    [
        (StatusCategory.NotStarted, StatusColor.Gray),
        (StatusCategory.InProgress, StatusColor.Blue),
        (StatusCategory.Done, StatusColor.Green),
        (StatusCategory.Canceled, StatusColor.Red),
    ];

    /// <summary>プロジェクトの全タスクのステータス別件数（#63）。</summary>
    public async Task<TaskStatusSummaryResponse> GetForProjectAsync(string projectKey, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.ViewProject, cancellationToken);
        var pid = access.ProjectId;
        var counts = await db.TaskItems.AsNoTracking().Where(t => t.ProjectId == pid)
            .GroupBy(t => t.StatusId)
            .Select(g => new { StatusId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StatusId, x => x.Count, cancellationToken);
        return await ByStatusAsync(pid, counts, cancellationToken);
    }

    /// <summary>
    /// 自分が担当のタスクのステータス別件数（#64）。projectKey なしは参加中・非アーカイブの全プロジェクトを区分別に、
    /// projectKey ありはそのプロジェクトのステータス別に集計する（OD-34）。
    /// </summary>
    public async Task<TaskStatusSummaryResponse> GetForMeAsync(string? projectKey, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        if (!string.IsNullOrWhiteSpace(projectKey))
        {
            var access = await guard.RequireAsync(projectKey, ProjectPermission.ViewProject, cancellationToken);
            var pid = access.ProjectId;
            var mine = await db.TaskItems.AsNoTracking().Where(t => t.ProjectId == pid && t.AssigneeUserId == userId)
                .GroupBy(t => t.StatusId)
                .Select(g => new { StatusId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.StatusId, x => x.Count, cancellationToken);
            return await ByStatusAsync(pid, mine, cancellationToken);
        }

        var activeProjectIds = db.Projects
            .Where(p => p.ArchivedAtUtc == null && db.ProjectMemberships.Any(m => m.ProjectId == p.Id && m.UserId == userId))
            .Select(p => p.Id);
        var byCategory = await db.TaskItems.AsNoTracking()
            .Where(t => t.AssigneeUserId == userId && activeProjectIds.Contains(t.ProjectId))
            .Join(db.WorkflowStatuses, t => t.StatusId, s => s.Id, (t, s) => s.Category)
            .GroupBy(category => category)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Category, x => x.Count, cancellationToken);

        var rows = Categories.Select((c, i) => (Id: (Guid?)null, Name: TaskLabels.Category(c.Category), c.Category, c.Color,
            SortOrder: i + 1, Count: byCategory.GetValueOrDefault(c.Category))).ToList();
        return Build(StatusSummaryGrouping.Category, rows);
    }

    /// <summary>プロジェクトの全ステータス（0 件を含む）を並び順で並べる（SR-04）。</summary>
    private async Task<TaskStatusSummaryResponse> ByStatusAsync(
        Guid projectId, IReadOnlyDictionary<Guid, int> counts, CancellationToken cancellationToken)
    {
        var statuses = await db.WorkflowStatuses.AsNoTracking().Where(s => s.ProjectId == projectId)
            .OrderBy(s => s.SortOrder)
            .Select(s => new { s.Id, s.Name, s.Category, s.Color, s.SortOrder })
            .ToListAsync(cancellationToken);
        var rows = statuses.Select(s => (Id: (Guid?)s.Id, s.Name, s.Category, s.Color, SortOrder: (int)s.SortOrder,
            Count: counts.GetValueOrDefault(s.Id))).ToList();
        return Build(StatusSummaryGrouping.Status, rows);
    }

    private static TaskStatusSummaryResponse Build(
        StatusSummaryGrouping grouping,
        IReadOnlyList<(Guid? Id, string Name, StatusCategory Category, StatusColor Color, int SortOrder, int Count)> rows)
    {
        var percents = ShareRounding.Percentages(rows.Select(r => r.Count).ToList());
        var items = rows.Select((r, i) => new StatusCountResponse(r.Id, r.Name, r.Category, r.Color, r.SortOrder, r.Count, percents[i]))
            .ToList();
        return new TaskStatusSummaryResponse(grouping, rows.Sum(r => r.Count), items);
    }
}
