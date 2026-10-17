using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Common;
using TaskManager.Application.Notices;
using TaskManager.Application.Security;
using TaskManager.Domain.Common;
using TaskManager.Domain.Projects;
using TaskManager.Domain.Tasks;

namespace TaskManager.Application.Tasks;

/// <summary>タスク（REQ-TSK-01〜06）。</summary>
public sealed class TaskItemService(
    IAppDbContext db,
    ICurrentUser currentUser,
    ProjectAccessGuard guard,
    ITaskNumberAllocator numberAllocator,
    IAttachmentStorage attachmentStorage,
    TaskProjection projection,
    NoticeWriter noticeWriter,
    TimeProvider timeProvider)
{
    public const int DefaultPageSize = 50;

    private static readonly HashSet<string> SortKeys = new(StringComparer.Ordinal)
    {
        "-updatedAt", "updatedAt", "number", "-number", "dueDate", "-dueDate", "priority", "-priority",
        "status", "title", "assignee", "-createdAt",
    };

    public async Task<PagedResponse<TaskSummaryResponse>> ListAsync(
        string projectKey, TaskListQuery q, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.ViewProject, cancellationToken);
        var sort = q.Sort ?? "-updatedAt";
        if (!SortKeys.Contains(sort))
        {
            throw new AppException(AppErrorKind.BadRequest, ErrorCodes.InvalidSort, "指定された並べ替え項目は使用できません。");
        }

        var pid = access.ProjectId;
        var today = await TodayAsync(cancellationToken);
        var query = db.TaskItems.AsNoTracking().Where(t => t.ProjectId == pid);

        var categories = q.Category is { Count: > 0 } c
            ? c.ToList()
            : q.IncludeClosed == true
                ? [StatusCategory.NotStarted, StatusCategory.InProgress, StatusCategory.Done, StatusCategory.Canceled]
                : [StatusCategory.NotStarted, StatusCategory.InProgress];
        var categoryStatusIds = db.WorkflowStatuses.Where(s => s.ProjectId == pid && categories.Contains(s.Category)).Select(s => s.Id);
        query = query.Where(t => categoryStatusIds.Contains(t.StatusId));

        if (q.CompletedSince is { } since)
        {
            var doneIds = db.WorkflowStatuses.Where(s => s.ProjectId == pid && s.Category == StatusCategory.Done).Select(s => s.Id);
            var sinceUtc = since.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(since, DateTimeKind.Utc) : since.ToUniversalTime();
            query = query.Where(t => !doneIds.Contains(t.StatusId) || t.CompletedAtUtc >= sinceUtc);
        }

        if (q.StatusId is { Count: > 0 } statusIds)
        {
            query = query.Where(t => statusIds.Contains(t.StatusId));
        }

        var assignees = q.AssigneeUserId?.ToList() ?? [];
        if (q.AssignedToMe == true)
        {
            assignees.Add(currentUser.UserId);
        }

        if (assignees.Count > 0 && q.Unassigned == true)
        {
            query = query.Where(t => t.AssigneeUserId == null || assignees.Contains(t.AssigneeUserId.Value));
        }
        else if (assignees.Count > 0)
        {
            query = query.Where(t => t.AssigneeUserId != null && assignees.Contains(t.AssigneeUserId.Value));
        }
        else if (q.Unassigned == true)
        {
            query = query.Where(t => t.AssigneeUserId == null);
        }

        if (q.Priority is { Count: > 0 } priorities)
        {
            query = query.Where(t => priorities.Contains(t.Priority));
        }

        if (q.TagId is { Count: > 0 } tagIds)
        {
            query = query.Where(t => db.TaskItemTags.Any(x => x.TaskId == t.Id && tagIds.Contains(x.TagId)));
        }

        if (q.MilestoneId is { } milestoneId)
        {
            query = query.Where(t => t.MilestoneId == milestoneId);
        }
        else if (q.NoMilestone == true)
        {
            query = query.Where(t => t.MilestoneId == null);
        }

        if (q.DueFrom is { } from)
        {
            query = query.Where(t => t.DueDate != null && t.DueDate >= from);
        }

        if (q.DueTo is { } to)
        {
            query = query.Where(t => t.DueDate != null && t.DueDate <= to);
        }

        if (q.Overdue == true)
        {
            var openIds = db.WorkflowStatuses
                .Where(s => s.ProjectId == pid && (s.Category == StatusCategory.NotStarted || s.Category == StatusCategory.InProgress))
                .Select(s => s.Id);
            query = query.Where(t => t.DueDate != null && t.DueDate < today && openIds.Contains(t.StatusId));
        }

        if (q.ParentTaskNumber is { } parentNumber)
        {
            var parentId = db.TaskItems.Where(p => p.ProjectId == pid && p.Number == parentNumber).Select(p => p.Id);
            query = query.Where(t => t.ParentTaskId != null && parentId.Contains(t.ParentTaskId.Value));
        }
        else if (q.TopLevelOnly == true)
        {
            query = query.Where(t => t.ParentTaskId == null);
        }

        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var term = q.Q.Trim();
            query = query.Where(t => t.Title.Contains(term));
        }

        query = Sort(query, sort);
        var page = PageRequest.Of(q.Page, q.PageSize, DefaultPageSize);
        var total = await query.CountAsync(cancellationToken);
        var tasks = await query.Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken);
        var items = (await projection.BuildAsync(tasks, today, cancellationToken)).Select(TaskProjection.WithoutProject).ToList();
        return PagedResponse<TaskSummaryResponse>.Create(items, page, total);
    }

    public async Task<TaskDetailResponse> GetAsync(string projectKey, int taskNumber, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.ViewProject, cancellationToken);
        var task = await db.TaskItems.AsNoTracking()
            .SingleOrDefaultAsync(t => t.ProjectId == access.ProjectId && t.Number == taskNumber, cancellationToken)
            ?? throw Errors.NotFound();
        return await BuildDetailAsync(task, access, cancellationToken);
    }

    public async Task<TaskDetailResponse> CreateAsync(string projectKey, CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.CreateTasks, cancellationToken);
        var pid = access.ProjectId;
        var status = request.StatusId is { } sid
            ? await db.WorkflowStatuses.SingleOrDefaultAsync(s => s.ProjectId == pid && s.Id == sid, cancellationToken)
              ?? throw Errors.Validation("statusId", "選択したステータスが見つかりません。")
            : await db.WorkflowStatuses.SingleAsync(s => s.ProjectId == pid && s.IsDefault, cancellationToken);
        var assignee = await ResolveAssigneeAsync(pid, request.AssigneeUserId, cancellationToken);
        var tags = await ResolveTagsAsync(pid, request.TagIds, cancellationToken);
        var milestone = await ResolveMilestoneAsync(pid, request.MilestoneId, null, cancellationToken);
        var parent = await ResolveParentAsync(pid, request.ParentTaskNumber, cancellationToken);
        var now = timeProvider.UtcNow();
        var actorId = currentUser.UserId;

        var taskId = await db.InTransactionAsync(async ct =>
        {
            var number = await numberAllocator.AllocateAsync(pid, ct) ?? throw Errors.ProjectArchived();
            var task = Wrap(() =>
            {
                var t = TaskItem.Create(pid, number, request.Title, status, request.Priority ?? TaskPriority.Normal, now);
                t.ChangeDescription(request.Description);
                t.ChangeDates(request.StartDate, request.DueDate);
                t.AssignTo(assignee?.Id);
                t.ChangeMilestone(milestone);
                t.ChangeParent(parent, hasSubtasks: false);
                t.ReplaceTags(tags);
                return t;
            });
            db.TaskItems.Add(task);
            db.TaskHistoryEntries.Add(TaskHistoryEntry.Create(task.Id, TaskHistoryKind.Created, actorId, null, task.Title, now));
            if (assignee is not null)
            {
                await NotifyAssignedAsync(access, task, assignee.Id, ct);
            }

            await db.SaveChangesAsync(ct);
            return task.Id;
        }, cancellationToken);

        var created = await db.TaskItems.AsNoTracking().SingleAsync(t => t.Id == taskId, cancellationToken);
        return await BuildDetailAsync(created, access, cancellationToken);
    }

    public async Task<TaskDetailResponse> UpdateAsync(
        string projectKey, int taskNumber, UpdateTaskRequest request, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.EditTasks, cancellationToken);
        var pid = access.ProjectId;
        var task = await db.TaskItems.Include(t => t.Tags)
            .SingleOrDefaultAsync(t => t.ProjectId == pid && t.Number == taskNumber, cancellationToken)
            ?? throw Errors.NotFound();
        db.SetExpectedVersion(task, request.Version);
        if (task.RowVersion != request.Version)
        {
            throw Errors.ConcurrencyConflict();
        }

        var now = timeProvider.UtcNow();
        var actorId = currentUser.UserId;
        var history = new List<TaskHistoryEntry>();
        void Record(TaskHistoryKind kind, string? oldValue, string? newValue)
        {
            if (oldValue != newValue)
            {
                history.Add(TaskHistoryEntry.Create(task.Id, kind, actorId, oldValue, newValue, now));
            }
        }

        var currentStatus = await db.WorkflowStatuses.AsNoTracking().SingleAsync(s => s.Id == task.StatusId, cancellationToken);
        Guid? newAssigneeToNotify = null;
        var completed = false;

        if (request.IsSpecified(nameof(request.Title)))
        {
            var old = task.Title;
            Wrap(() => task.ChangeTitle(request.Title ?? string.Empty));
            Record(TaskHistoryKind.TitleChanged, old, task.Title);
        }

        if (request.IsSpecified(nameof(request.Description)))
        {
            var old = task.Description;
            Wrap(() => task.ChangeDescription(request.Description));
            if (old != task.Description)
            {
                history.Add(TaskHistoryEntry.Create(task.Id, TaskHistoryKind.DescriptionChanged, actorId, null, null, now));
            }
        }

        if (request.IsSpecified(nameof(request.StatusId)) && request.StatusId is { } statusId && statusId != task.StatusId)
        {
            var status = await db.WorkflowStatuses.SingleOrDefaultAsync(s => s.ProjectId == pid && s.Id == statusId, cancellationToken)
                ?? throw Errors.Validation("statusId", "選択したステータスが見つかりません。");
            Wrap(() => task.ChangeStatus(status, currentStatus.Category, now));
            Record(TaskHistoryKind.StatusChanged, currentStatus.Name, status.Name);
            completed = status.Category == StatusCategory.Done && currentStatus.Category != StatusCategory.Done;
        }
        else if (request.IsSpecified(nameof(request.StatusId)) && request.StatusId is null)
        {
            throw Errors.Validation("statusId", "ステータスを選んでください。");
        }

        if (request.IsSpecified(nameof(request.Priority)))
        {
            var priority = request.Priority ?? throw Errors.Validation("priority", "優先度を選んでください。");
            var old = task.Priority;
            task.ChangePriority(priority);
            Record(TaskHistoryKind.PriorityChanged, TaskLabels.Priority(old), TaskLabels.Priority(priority));
        }

        if (request.IsSpecified(nameof(request.AssigneeUserId)) && request.AssigneeUserId != task.AssigneeUserId)
        {
            var assignee = await ResolveAssigneeAsync(pid, request.AssigneeUserId, cancellationToken);
            var oldName = task.AssigneeUserId is { } oldId
                ? await db.UserAccounts.AsNoTracking().Where(u => u.Id == oldId).Select(u => u.DisplayName).SingleOrDefaultAsync(cancellationToken)
                : null;
            task.AssignTo(assignee?.Id);
            Record(TaskHistoryKind.AssigneeChanged, oldName, assignee?.DisplayName);
            newAssigneeToNotify = assignee?.Id;
        }

        if (request.IsSpecified(nameof(request.StartDate)) || request.IsSpecified(nameof(request.DueDate)))
        {
            var oldStart = task.StartDate;
            var oldDue = task.DueDate;
            var start = request.IsSpecified(nameof(request.StartDate)) ? request.StartDate : task.StartDate;
            var due = request.IsSpecified(nameof(request.DueDate)) ? request.DueDate : task.DueDate;
            Wrap(() => task.ChangeDates(start, due));
            Record(TaskHistoryKind.StartDateChanged, TaskLabels.Date(oldStart), TaskLabels.Date(task.StartDate));
            Record(TaskHistoryKind.DueDateChanged, TaskLabels.Date(oldDue), TaskLabels.Date(task.DueDate));
        }

        if (request.IsSpecified(nameof(request.MilestoneId)) && request.MilestoneId != task.MilestoneId)
        {
            var milestone = await ResolveMilestoneAsync(pid, request.MilestoneId, task.MilestoneId, cancellationToken);
            var oldName = task.MilestoneId is { } oldId
                ? await db.Milestones.AsNoTracking().Where(m => m.Id == oldId).Select(m => m.Name).SingleOrDefaultAsync(cancellationToken)
                : null;
            Wrap(() => task.ChangeMilestone(milestone));
            Record(TaskHistoryKind.MilestoneChanged, oldName, milestone?.Name);
        }

        if (request.IsSpecified(nameof(request.ParentTaskNumber)))
        {
            var parent = await ResolveParentAsync(pid, request.ParentTaskNumber, cancellationToken);
            if (parent?.Id != task.ParentTaskId)
            {
                var hasSubtasks = await db.TaskItems.AnyAsync(t => t.ParentTaskId == task.Id, cancellationToken);
                var oldKey = task.ParentTaskId is { } oldParentId
                    ? await db.TaskItems.AsNoTracking().Where(t => t.Id == oldParentId).Select(t => (int?)t.Number).SingleOrDefaultAsync(cancellationToken)
                    : null;
                Wrap(() => task.ChangeParent(parent, hasSubtasks));
                Record(TaskHistoryKind.ParentChanged,
                    oldKey is { } ok ? TaskKey.Format(access.ProjectKey, ok) : null,
                    parent is null ? null : TaskKey.Format(access.ProjectKey, parent.Number));
            }
        }

        if (request.IsSpecified(nameof(request.TagIds)))
        {
            var tags = await ResolveTagsAsync(pid, request.TagIds, cancellationToken);
            var oldTagIds = task.Tags.Select(x => x.TagId).ToList();
            var oldNames = await db.Tags.AsNoTracking().Where(t => oldTagIds.Contains(t.Id)).Select(t => t.Name).ToListAsync(cancellationToken);
            if (Wrap(() => task.ReplaceTags(tags)))
            {
                Record(TaskHistoryKind.TagsChanged, JoinNames(oldNames), JoinNames(tags.Select(t => t.Name)));
            }
        }

        db.TaskHistoryEntries.AddRange(history);
        if (newAssigneeToNotify is { } assigneeId)
        {
            await NotifyAssignedAsync(access, task, assigneeId, cancellationToken);
        }

        if (completed && task.CreatedByUserId is { } creatorId)
        {
            await noticeWriter.NotifyAsync(creatorId, NoticeKind.TaskCompleted, actorId, pid, task.Id,
                $"{NoticeWriter.TaskLabel(access.ProjectKey, task.Number, task.Title)}が完了しました",
                NoticeWriter.TaskLink(access.ProjectKey, task.Number), cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        var updated = await db.TaskItems.AsNoTracking().SingleAsync(t => t.Id == task.Id, cancellationToken);
        return await BuildDetailAsync(updated, access, cancellationToken);
    }

    public async Task DeleteAsync(string projectKey, int taskNumber, bool includeSubtasks, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.DeleteOwnTask, cancellationToken);
        var task = await db.TaskItems.AsNoTracking()
            .SingleOrDefaultAsync(t => t.ProjectId == access.ProjectId && t.Number == taskNumber, cancellationToken)
            ?? throw Errors.NotFound();
        if (!access.Has(ProjectPermission.DeleteAnyTask) && task.CreatedByUserId != currentUser.UserId)
        {
            throw Errors.Forbidden();
        }

        var childIds = await db.TaskItems.Where(t => t.ParentTaskId == task.Id).Select(t => t.Id).ToListAsync(cancellationToken);
        if (childIds.Count > 0 && !includeSubtasks)
        {
            throw Errors.Conflict(ErrorCodes.TaskHasSubtasks, "サブタスクがあるため削除できません。",
                "サブタスクもまとめて削除するか、先にサブタスクを移動してください。");
        }

        var ids = childIds.Append(task.Id).ToList();
        var storageKeys = await db.TaskAttachments.Where(a => ids.Contains(a.TaskId)).Select(a => a.StorageKey).ToListAsync(cancellationToken);
        await db.InTransactionAsync(async ct =>
        {
            await db.TaskItemTags.Where(x => ids.Contains(x.TaskId)).ExecuteDeleteAsync(ct);
            await db.TaskHistoryEntries.Where(x => ids.Contains(x.TaskId)).ExecuteDeleteAsync(ct);
            await db.TaskComments.Where(x => ids.Contains(x.TaskId)).ExecuteDeleteAsync(ct);
            await db.TaskAttachments.Where(x => ids.Contains(x.TaskId)).ExecuteDeleteAsync(ct);
            await db.Notices.Where(n => n.TaskId != null && ids.Contains(n.TaskId.Value))
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.TaskId, (Guid?)null), ct);
            await db.TaskItems.Where(t => childIds.Contains(t.Id)).ExecuteDeleteAsync(ct);
            await db.TaskItems.Where(t => t.Id == task.Id).ExecuteDeleteAsync(ct);
            return true;
        }, cancellationToken);

        foreach (var key in storageKeys)
        {
            await attachmentStorage.DeleteAsync(key, cancellationToken);
        }
    }

    internal async Task<TaskDetailResponse> BuildDetailAsync(TaskItem task, ProjectAccessContext access, CancellationToken cancellationToken)
    {
        var today = await TodayAsync(cancellationToken);
        var summary = (await projection.BuildAsync([task], today, cancellationToken))[0];
        var subtasks = await db.TaskItems.AsNoTracking().Where(t => t.ParentTaskId == task.Id).OrderBy(t => t.Number)
            .ToListAsync(cancellationToken);
        var subtaskSummaries = (await projection.BuildAsync(subtasks, today, cancellationToken)).Select(TaskProjection.WithoutProject).ToList();
        var userIds = new[] { task.CreatedByUserId, task.UpdatedByUserId }.Where(id => id != null).Select(id => id!.Value).Distinct().ToList();
        var users = await db.UserAccounts.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new UserRef(u.Id, u.DisplayName, u.IsActive), cancellationToken);
        var writable = !access.IsArchived;
        var permissions = new TaskPermissions(
            CanEdit: writable && access.Has(ProjectPermission.EditTasks),
            CanDelete: writable && (access.Has(ProjectPermission.DeleteAnyTask)
                || (access.Has(ProjectPermission.DeleteOwnTask) && task.CreatedByUserId == currentUser.UserId)),
            CanComment: writable && access.Has(ProjectPermission.Comment),
            CanUpload: writable && access.Has(ProjectPermission.UploadAttachments));
        return new TaskDetailResponse(
            summary.Key, summary.Number, summary.Title, summary.Status, summary.Priority, summary.Assignee, summary.StartDate,
            summary.DueDate, summary.IsOverdue, summary.Milestone, summary.Tags, summary.Parent, summary.SubtaskCount,
            summary.ClosedSubtaskCount, summary.CommentCount, summary.AttachmentCount, summary.UpdatedAtUtc, summary.Version,
            task.Description, task.CompletedAtUtc, subtaskSummaries, task.CreatedAtUtc,
            task.CreatedByUserId is { } c ? users.GetValueOrDefault(c) : null,
            task.UpdatedByUserId is { } u ? users.GetValueOrDefault(u) : null,
            permissions);
    }

    internal async Task<DateOnly> TodayAsync(CancellationToken cancellationToken)
    {
        var tz = await db.UserAccounts.AsNoTracking().Where(u => u.Id == currentUser.UserId).Select(u => u.TimeZoneId)
            .SingleOrDefaultAsync(cancellationToken);
        return timeProvider.TodayIn(tz ?? TimeZones.Default);
    }

    private IQueryable<TaskItem> Sort(IQueryable<TaskItem> query, string sort) => sort switch
    {
        "updatedAt" => query.OrderBy(t => t.UpdatedAtUtc).ThenByDescending(t => t.Number),
        "number" => query.OrderBy(t => t.Number),
        "-number" => query.OrderByDescending(t => t.Number),
        "dueDate" => query.OrderBy(t => t.DueDate == null).ThenBy(t => t.DueDate).ThenByDescending(t => t.Number),
        "-dueDate" => query.OrderBy(t => t.DueDate == null).ThenByDescending(t => t.DueDate).ThenByDescending(t => t.Number),
        "priority" => query.OrderBy(t => t.Priority).ThenByDescending(t => t.Number),
        "-priority" => query.OrderByDescending(t => t.Priority).ThenByDescending(t => t.Number),
        "status" => query
            .OrderBy(t => db.WorkflowStatuses.Where(s => s.Id == t.StatusId).Select(s => s.SortOrder).FirstOrDefault())
            .ThenByDescending(t => t.Number),
        "title" => query.OrderBy(t => t.Title).ThenByDescending(t => t.Number),
        "assignee" => query
            .OrderBy(t => t.AssigneeUserId == null)
            .ThenBy(t => db.UserAccounts.Where(u => u.Id == t.AssigneeUserId).Select(u => u.DisplayName).FirstOrDefault())
            .ThenByDescending(t => t.Number),
        "-createdAt" => query.OrderByDescending(t => t.CreatedAtUtc).ThenByDescending(t => t.Number),
        _ => query.OrderByDescending(t => t.UpdatedAtUtc).ThenByDescending(t => t.Number),
    };

    private async Task NotifyAssignedAsync(ProjectAccessContext access, TaskItem task, Guid assigneeId, CancellationToken cancellationToken)
    {
        var actorName = await db.UserAccounts.AsNoTracking().Where(u => u.Id == currentUser.UserId).Select(u => u.DisplayName)
            .SingleOrDefaultAsync(cancellationToken);
        await noticeWriter.NotifyAsync(assigneeId, NoticeKind.TaskAssigned, currentUser.UserId, access.ProjectId, task.Id,
            $"{actorName}さんがあなたを {NoticeWriter.TaskLabel(access.ProjectKey, task.Number, task.Title)} の担当者にしました",
            NoticeWriter.TaskLink(access.ProjectKey, task.Number), cancellationToken);
    }

    /// <summary>M-03: 担当者は同一プロジェクトの編集者以上で、有効な利用者。</summary>
    private async Task<Domain.Accounts.UserAccount?> ResolveAssigneeAsync(Guid projectId, Guid? userId, CancellationToken cancellationToken)
    {
        if (userId is not { } id)
        {
            return null;
        }

        var ok = await db.ProjectMemberships.AnyAsync(
            m => m.ProjectId == projectId && m.UserId == id && m.Role >= ProjectRole.Editor, cancellationToken);
        var user = ok ? await db.UserAccounts.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id && u.IsActive, cancellationToken) : null;
        return user ?? throw Errors.Validation("assigneeUserId",
            "担当者には、このプロジェクトの編集者または管理者で、利用中の人を選んでください。");
    }

    private async Task<List<Tag>> ResolveTagsAsync(Guid projectId, IReadOnlyList<Guid>? tagIds, CancellationToken cancellationToken)
    {
        if (tagIds is not { Count: > 0 })
        {
            return [];
        }

        if (tagIds.Distinct().Count() != tagIds.Count)
        {
            throw Errors.Validation("tagIds", "同じタグが重複しています。");
        }

        var tags = await db.Tags.AsNoTracking().Where(t => t.ProjectId == projectId && tagIds.Contains(t.Id)).ToListAsync(cancellationToken);
        return tags.Count == tagIds.Count ? tags : throw Errors.Validation("tagIds", "選択したタグが見つかりません。");
    }

    private async Task<Milestone?> ResolveMilestoneAsync(Guid projectId, Guid? milestoneId, Guid? currentMilestoneId, CancellationToken cancellationToken)
    {
        if (milestoneId is not { } id)
        {
            return null;
        }

        var milestone = await db.Milestones.AsNoTracking().SingleOrDefaultAsync(m => m.ProjectId == projectId && m.Id == id, cancellationToken)
            ?? throw Errors.Validation("milestoneId", "選択したマイルストーンが見つかりません。");
        if (milestone.State == MilestoneState.Closed && id != currentMilestoneId)
        {
            throw Errors.Conflict(ErrorCodes.MilestoneClosed, "終了したマイルストーンにはタスクを追加できません。");
        }

        return milestone;
    }

    private async Task<TaskItem?> ResolveParentAsync(Guid projectId, int? parentNumber, CancellationToken cancellationToken)
    {
        if (parentNumber is not { } number)
        {
            return null;
        }

        return await db.TaskItems.AsNoTracking().SingleOrDefaultAsync(t => t.ProjectId == projectId && t.Number == number, cancellationToken)
            ?? throw Errors.Validation("parentTaskNumber", "選択した親タスクが見つかりません。");
    }

    private static string? JoinNames(IEnumerable<string> names)
    {
        var list = names.OrderBy(n => n, StringComparer.Ordinal).ToList();
        return list.Count == 0 ? null : string.Join("、", list);
    }

    private static T Wrap<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch (DomainException ex)
        {
            throw Errors.FromDomain(ex);
        }
    }

    private static void Wrap(Action action) => Wrap(() =>
    {
        action();
        return true;
    });
}
