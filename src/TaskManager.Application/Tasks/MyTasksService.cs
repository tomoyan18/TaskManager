using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Common;
using TaskManager.Domain.Common;
using TaskManager.Domain.Tasks;

namespace TaskManager.Application.Tasks;

/// <summary>自分のタスク（REQ-MY-01）。期限区分は利用者のタイムゾーンで判定する。</summary>
public sealed class MyTasksService(IAppDbContext db, ICurrentUser currentUser, TaskProjection projection, TimeProvider timeProvider)
{
    public const int MaxTasks = 500;
    public static readonly TimeSpan RecentlyCompletedWindow = TimeSpan.FromDays(7);

    public async Task<MyTasksResponse> GetAsync(bool includeRecentlyCompleted, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        var timeZoneId = await db.UserAccounts.AsNoTracking().Where(u => u.Id == userId).Select(u => u.TimeZoneId)
            .SingleOrDefaultAsync(cancellationToken) ?? TimeZones.Default;
        var today = timeProvider.TodayIn(timeZoneId);

        var activeProjectIds = db.Projects
            .Where(p => p.ArchivedAtUtc == null && db.ProjectMemberships.Any(m => m.ProjectId == p.Id && m.UserId == userId))
            .Select(p => p.Id);
        var mine = db.TaskItems.AsNoTracking().Where(t => t.AssigneeUserId == userId && activeProjectIds.Contains(t.ProjectId));

        var openStatusIds = db.WorkflowStatuses
            .Where(s => s.Category == StatusCategory.NotStarted || s.Category == StatusCategory.InProgress).Select(s => s.Id);
        var open = await mine.Where(t => openStatusIds.Contains(t.StatusId))
            .OrderBy(t => t.DueDate == null).ThenBy(t => t.DueDate).ThenByDescending(t => t.Priority)
            .Take(MaxTasks + 1).ToListAsync(cancellationToken);
        var truncated = open.Count > MaxTasks;
        if (truncated)
        {
            open.RemoveAt(open.Count - 1);
        }

        var summaries = await projection.BuildAsync(open, today, cancellationToken);
        var grouped = summaries
            .GroupBy(s => DueBucketClassifier.Classify(s.DueDate, today))
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<TaskSummaryWithProjectResponse>)g
                    .OrderByDescending(s => s.Priority).ThenBy(s => s.DueDate).ThenBy(s => s.Number).ToList());

        IReadOnlyList<TaskSummaryWithProjectResponse> recent = [];
        if (includeRecentlyCompleted)
        {
            var since = timeProvider.UtcNow() - RecentlyCompletedWindow;
            var done = await mine.Where(t => t.CompletedAtUtc != null && t.CompletedAtUtc >= since)
                .Where(t => db.WorkflowStatuses.Any(s => s.Id == t.StatusId && s.Category == StatusCategory.Done))
                .OrderByDescending(t => t.CompletedAtUtc).Take(100).ToListAsync(cancellationToken);
            recent = await projection.BuildAsync(done, today, cancellationToken);
        }

        IReadOnlyList<TaskSummaryWithProjectResponse> Bucket(DueBucket b) => grouped.GetValueOrDefault(b) ?? [];
        return new MyTasksResponse(
            today,
            timeZoneId,
            new MyTaskBuckets(Bucket(DueBucket.Overdue), Bucket(DueBucket.Today), Bucket(DueBucket.ThisWeek), Bucket(DueBucket.Later), Bucket(DueBucket.NoDueDate)),
            recent,
            truncated);
    }
}
