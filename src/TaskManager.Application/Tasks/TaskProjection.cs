using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Common;
using TaskManager.Domain.Common;
using TaskManager.Domain.Tasks;

namespace TaskManager.Application.Tasks;

/// <summary>
/// タスク一覧・詳細の応答を組み立てる。関連（ステータス・担当者・タグ・件数など）はまとめて読み込み、
/// 行ごとの問い合わせ（N+1）を避ける。
/// </summary>
public sealed class TaskProjection(IAppDbContext db)
{
    public async Task<IReadOnlyList<TaskSummaryWithProjectResponse>> BuildAsync(
        IReadOnlyList<TaskItem> tasks, DateOnly today, CancellationToken cancellationToken)
    {
        if (tasks.Count == 0)
        {
            return [];
        }

        var taskIds = tasks.Select(t => t.Id).ToList();
        var projectIds = tasks.Select(t => t.ProjectId).Distinct().ToList();
        var statusIds = tasks.Select(t => t.StatusId).Distinct().ToList();
        var userIds = tasks.Where(t => t.AssigneeUserId != null).Select(t => t.AssigneeUserId!.Value).Distinct().ToList();
        var milestoneIds = tasks.Where(t => t.MilestoneId != null).Select(t => t.MilestoneId!.Value).Distinct().ToList();
        var parentIds = tasks.Where(t => t.ParentTaskId != null).Select(t => t.ParentTaskId!.Value).Distinct().ToList();

        var projects = await db.Projects.AsNoTracking().Where(p => projectIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new ProjectRef(p.Key, p.Name), cancellationToken);
        var statuses = await db.WorkflowStatuses.AsNoTracking().Where(s => statusIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => new StatusRef(s.Id, s.Name, s.Category, s.Color), cancellationToken);
        var users = await db.UserAccounts.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new UserRef(u.Id, u.DisplayName, u.IsActive), cancellationToken);
        var milestones = await db.Milestones.AsNoTracking().Where(m => milestoneIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => new MilestoneRef(m.Id, m.Name, m.State), cancellationToken);
        var tagRows = await db.TaskItemTags.AsNoTracking().Where(x => taskIds.Contains(x.TaskId))
            .Join(db.Tags, x => x.TagId, t => t.Id, (x, t) => new { x.TaskId, t.Id, t.Name, t.Color })
            .ToListAsync(cancellationToken);
        var tags = tagRows.GroupBy(r => r.TaskId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<TagRef>)g.OrderBy(r => r.Name, StringComparer.Ordinal)
                .Select(r => new TagRef(r.Id, r.Name, r.Color)).ToList());
        var parents = await db.TaskItems.AsNoTracking().Where(t => parentIds.Contains(t.Id))
            .Join(db.WorkflowStatuses, t => t.StatusId, s => s.Id, (t, s) => new { t.Id, t.ProjectId, t.Number, t.Title, s.Category })
            .ToListAsync(cancellationToken);
        var parentRefs = parents.ToDictionary(
            p => p.Id,
            p => new TaskRef(TaskKey.Format(projects.GetValueOrDefault(p.ProjectId)?.Key ?? string.Empty, p.Number), p.Number, p.Title, p.Category));
        var subtaskCounts = await db.TaskItems.AsNoTracking()
            .Where(t => t.ParentTaskId != null && taskIds.Contains(t.ParentTaskId.Value))
            .Join(db.WorkflowStatuses, t => t.StatusId, s => s.Id, (t, s) => new { ParentId = t.ParentTaskId!.Value, s.Category })
            .GroupBy(x => x.ParentId)
            .Select(g => new
            {
                ParentId = g.Key,
                Total = g.Count(),
                Closed = g.Count(x => x.Category == StatusCategory.Done || x.Category == StatusCategory.Canceled),
            })
            .ToDictionaryAsync(x => x.ParentId, cancellationToken);
        var commentCounts = await db.TaskComments.AsNoTracking().Where(c => taskIds.Contains(c.TaskId))
            .GroupBy(c => c.TaskId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        var attachmentCounts = await db.TaskAttachments.AsNoTracking().Where(a => taskIds.Contains(a.TaskId))
            .GroupBy(a => a.TaskId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        return tasks.Select(t =>
        {
            var project = projects[t.ProjectId];
            var status = statuses[t.StatusId];
            subtaskCounts.TryGetValue(t.Id, out var sub);
            return new TaskSummaryWithProjectResponse(
                project,
                TaskKey.Format(project.Key, t.Number),
                t.Number,
                t.Title,
                status,
                t.Priority,
                t.AssigneeUserId is { } a ? users.GetValueOrDefault(a) : null,
                t.StartDate,
                t.DueDate,
                TaskItem.IsOverdue(t.DueDate, status.Category, today),
                t.MilestoneId is { } m ? milestones.GetValueOrDefault(m) : null,
                tags.GetValueOrDefault(t.Id) ?? [],
                t.ParentTaskId is { } p ? parentRefs.GetValueOrDefault(p) : null,
                sub?.Total ?? 0,
                sub?.Closed ?? 0,
                commentCounts.GetValueOrDefault(t.Id),
                attachmentCounts.GetValueOrDefault(t.Id),
                t.UpdatedAtUtc,
                t.RowVersion);
        }).ToList();
    }

    public static TaskSummaryResponse WithoutProject(TaskSummaryWithProjectResponse s) => new(
        s.Key, s.Number, s.Title, s.Status, s.Priority, s.Assignee, s.StartDate, s.DueDate, s.IsOverdue, s.Milestone,
        s.Tags, s.Parent, s.SubtaskCount, s.ClosedSubtaskCount, s.CommentCount, s.AttachmentCount, s.UpdatedAtUtc, s.Version);
}
