using TaskManager.Domain.Common;
using TaskManager.Domain.Projects;

namespace TaskManager.Domain.Tasks;

/// <summary>タスク（domain-model.md §4.8）。C# の Task と衝突しないよう TaskItem とする。</summary>
public sealed class TaskItem : IAuditable, IVersioned
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 20000;
    public const int MaxTags = 10;
    public static readonly DateOnly MinDate = new(2000, 1, 1);
    public static readonly DateOnly MaxDate = new(2099, 12, 31);

    private readonly List<TaskItemTag> _tags = [];

    private TaskItem()
    {
    }

    public Guid Id { get; private set; }

    public Guid ProjectId { get; private set; }

    public int Number { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public Guid StatusId { get; private set; }

    public TaskPriority Priority { get; private set; }

    public Guid? AssigneeUserId { get; private set; }

    public DateOnly? StartDate { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public Guid? MilestoneId { get; private set; }

    public Guid? ParentTaskId { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public uint RowVersion { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public IReadOnlyCollection<TaskItemTag> Tags => _tags;

    /// <summary>番号は ITaskNumberAllocator で採番した値を渡す（TK-01）。</summary>
    public static TaskItem Create(Guid projectId, int number, string title, WorkflowStatus status, TaskPriority priority, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);

        var task = new TaskItem
        {
            Id = Guid.CreateVersion7(),
            ProjectId = projectId,
            Number = number,
            Title = NormalizeTitle(title),
            Priority = priority,
        };
        task.ChangeStatus(status, StatusCategory.NotStarted, nowUtc);
        return task;
    }

    public static string NormalizeTitle(string? title) =>
        TextRules.RequireSingleLine(title, TitleMaxLength, "validation.failed", "title", "タイトル");

    public static string? NormalizeDescription(string? description) =>
        TextRules.OptionalMultiline(description, DescriptionMaxLength, "validation.failed", "description", "説明");

    public void ChangeTitle(string title) => Title = NormalizeTitle(title);

    public void ChangeDescription(string? description) => Description = NormalizeDescription(description);

    public void ChangePriority(TaskPriority priority) => Priority = priority;

    /// <summary>TK-05: 完了区分への変更で完了日時を設定し、完了以外への変更で消す。</summary>
    public void ChangeStatus(WorkflowStatus status, StatusCategory previousCategory, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(status);
        EnsureSameProject(status.ProjectId, "statusId");
        StatusId = status.Id;
        if (status.Category == StatusCategory.Done)
        {
            if (previousCategory != StatusCategory.Done || CompletedAtUtc is null)
            {
                CompletedAtUtc = nowUtc;
            }
        }
        else
        {
            CompletedAtUtc = null;
        }
    }

    /// <summary>TK-02: 開始日 ≤ 期限、かつ許容範囲内。</summary>
    public void ChangeDates(DateOnly? startDate, DateOnly? dueDate)
    {
        EnsureInRange(startDate, "startDate", "開始日");
        EnsureInRange(dueDate, "dueDate", "期限");
        if (startDate is { } s && dueDate is { } d && s > d)
        {
            throw new DomainException("validation.failed", "期限は開始日以降の日付にしてください。", "dueDate");
        }

        StartDate = startDate;
        DueDate = dueDate;
    }

    /// <summary>担当者の資格（同一プロジェクトの編集者以上・有効）は Application で確認済みであること（M-03）。</summary>
    public void AssignTo(Guid? userId) => AssigneeUserId = userId;

    public void ChangeMilestone(Milestone? milestone)
    {
        if (milestone is not null)
        {
            EnsureSameProject(milestone.ProjectId, "milestoneId");
        }

        MilestoneId = milestone?.Id;
    }

    /// <summary>TK-03: 親子は 1 段のみ。hasSubtasks は自分がサブタスクを持つか。</summary>
    public void ChangeParent(TaskItem? parent, bool hasSubtasks)
    {
        if (parent is null)
        {
            ParentTaskId = null;
            return;
        }

        EnsureSameProject(parent.ProjectId, "parentTaskNumber");
        if (parent.Id == Id)
        {
            throw new DomainException("validation.failed", "自分自身を親タスクにはできません。", "parentTaskNumber");
        }

        if (parent.ParentTaskId is not null)
        {
            throw new DomainException(
                "validation.failed", "サブタスクの下にサブタスクは作れません。親には通常のタスクを選んでください。", "parentTaskNumber");
        }

        if (hasSubtasks)
        {
            throw new DomainException(
                "validation.failed", "サブタスクを持つタスクは、別のタスクのサブタスクにできません。", "parentTaskNumber");
        }

        ParentTaskId = parent.Id;
    }

    /// <summary>TK-06: タグを置き換える（最大 10 件・重複なし）。変更があれば true。</summary>
    public bool ReplaceTags(IReadOnlyCollection<Tag> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        if (tags.Count > MaxTags)
        {
            throw new DomainException("validation.failed", $"タグは{MaxTags}個まで設定できます。", "tagIds");
        }

        foreach (var tag in tags)
        {
            EnsureSameProject(tag.ProjectId, "tagIds");
        }

        var newIds = tags.Select(t => t.Id).ToHashSet();
        var currentIds = _tags.Select(t => t.TagId).ToHashSet();
        if (newIds.SetEquals(currentIds))
        {
            return false;
        }

        _tags.RemoveAll(t => !newIds.Contains(t.TagId));
        foreach (var id in newIds.Where(id => !currentIds.Contains(id)))
        {
            _tags.Add(new TaskItemTag(Id, id));
        }

        return true;
    }

    /// <summary>期限切れか（終了区分でなく、期限が今日より前）。</summary>
    public static bool IsOverdue(DateOnly? dueDate, StatusCategory category, DateOnly today) =>
        dueDate is { } due && !category.IsClosed() && due < today;

    private static void EnsureInRange(DateOnly? date, string field, string label)
    {
        if (date is { } value && (value < MinDate || value > MaxDate))
        {
            throw new DomainException("validation.failed", $"{label}は 2000年1月1日〜2099年12月31日の範囲で指定してください。", field);
        }
    }

    private void EnsureSameProject(Guid projectId, string field)
    {
        if (projectId != ProjectId)
        {
            throw new DomainException("validation.failed", "同じプロジェクトの項目を選んでください。", field);
        }
    }
}
