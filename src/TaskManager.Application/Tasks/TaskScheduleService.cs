using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Common;
using TaskManager.Application.Security;
using TaskManager.Domain.Common;
using TaskManager.Domain.Tasks;

namespace TaskManager.Application.Tasks;

/// <summary>ガントチャートの日程（REQ-GNT-01）。期間と重なるタスクと、日付が未設定のタスクを返す。</summary>
public sealed class TaskScheduleService(
    IAppDbContext db, ICurrentUser currentUser, ProjectAccessGuard guard, TaskProjection projection, TimeProvider timeProvider)
{
    /// <summary>期間と重なるタスクの上限（超えた場合は Truncated）。</summary>
    public const int MaxItems = 500;

    /// <summary>日付未設定のタスクを返す上限（件数は別に返す）。</summary>
    public const int MaxUndatedItems = 50;

    public async Task<TaskScheduleResponse> GetAsync(string projectKey, TaskScheduleQuery q, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(q);
        var access = await guard.RequireAsync(projectKey, ProjectPermission.ViewProject, cancellationToken);
        if (q.From is not { } fromDate)
        {
            throw Errors.Validation("from", "表示期間の開始日を入力してください。");
        }

        if (q.To is not { } toDate)
        {
            throw Errors.Validation("to", "表示期間の終了日を入力してください。");
        }

        ScheduleWindow window;
        try
        {
            window = ScheduleWindow.Create(fromDate, toDate);
        }
        catch (DomainException ex)
        {
            throw Errors.FromDomain(ex);
        }

        var pid = access.ProjectId;
        var timeZoneId = await db.UserAccounts.AsNoTracking().Where(u => u.Id == currentUser.UserId).Select(u => u.TimeZoneId)
            .SingleOrDefaultAsync(cancellationToken) ?? TimeZones.Default;
        var today = timeProvider.TodayIn(timeZoneId);

        StatusCategory[] categories = q.IncludeClosed == true
            ? [StatusCategory.NotStarted, StatusCategory.InProgress, StatusCategory.Done, StatusCategory.Canceled]
            : [StatusCategory.NotStarted, StatusCategory.InProgress];
        var statusIds = db.WorkflowStatuses.Where(s => s.ProjectId == pid && categories.Contains(s.Category)).Select(s => s.Id);
        var tasks = db.TaskItems.AsNoTracking().Where(t => t.ProjectId == pid && statusIds.Contains(t.StatusId));

        // SC-02: 棒の範囲（開始日・期限の片方だけなら 1 日）が期間と重なる。SQL では COALESCE になる
        var from = window.From;
        var to = window.To;
        var scheduled = await tasks
            .Where(t => (t.StartDate ?? t.DueDate) <= to && (t.DueDate ?? t.StartDate) >= from)
            .OrderBy(t => t.StartDate ?? t.DueDate).ThenBy(t => t.Number)
            .Take(MaxItems + 1)
            .ToListAsync(cancellationToken);
        var truncated = scheduled.Count > MaxItems;
        if (truncated)
        {
            scheduled.RemoveAt(scheduled.Count - 1);
        }

        // SC-03: 開始日・期限がどちらもないタスク
        var undatedQuery = tasks.Where(t => t.StartDate == null && t.DueDate == null);
        var undatedCount = await undatedQuery.CountAsync(cancellationToken);
        var undated = await undatedQuery.OrderByDescending(t => t.Priority).ThenBy(t => t.Number)
            .Take(MaxUndatedItems).ToListAsync(cancellationToken);

        var items = (await projection.BuildAsync(scheduled, today, cancellationToken)).Select(TaskProjection.WithoutProject).ToList();
        var undatedItems = (await projection.BuildAsync(undated, today, cancellationToken)).Select(TaskProjection.WithoutProject).ToList();
        return new TaskScheduleResponse(window.From, window.To, today, timeZoneId, items, truncated, undatedItems, undatedCount);
    }
}
