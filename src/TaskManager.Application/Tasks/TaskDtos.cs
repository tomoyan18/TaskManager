using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using TaskManager.Application.Common;
using TaskManager.Domain.Common;

namespace TaskManager.Application.Tasks;

/// <summary>タスク一覧の 1 行（docs/design/api-design.md #41）。</summary>
public sealed record TaskSummaryResponse(
    string Key,
    int Number,
    string Title,
    StatusRef Status,
    TaskPriority Priority,
    UserRef? Assignee,
    DateOnly? StartDate,
    DateOnly? DueDate,
    bool IsOverdue,
    MilestoneRef? Milestone,
    IReadOnlyList<TagRef> Tags,
    TaskRef? Parent,
    int SubtaskCount,
    int ClosedSubtaskCount,
    int CommentCount,
    int AttachmentCount,
    DateTime UpdatedAtUtc,
    uint Version);

/// <summary>画面の表示制御用の権限（操作の可否の最終判断は API が行う）。</summary>
public sealed record TaskPermissions(bool CanEdit, bool CanDelete, bool CanComment, bool CanUpload);

/// <summary>タスク詳細（#43）。</summary>
public sealed record TaskDetailResponse(
    string Key,
    int Number,
    string Title,
    StatusRef Status,
    TaskPriority Priority,
    UserRef? Assignee,
    DateOnly? StartDate,
    DateOnly? DueDate,
    bool IsOverdue,
    MilestoneRef? Milestone,
    IReadOnlyList<TagRef> Tags,
    TaskRef? Parent,
    int SubtaskCount,
    int ClosedSubtaskCount,
    int CommentCount,
    int AttachmentCount,
    DateTime UpdatedAtUtc,
    uint Version,
    string? Description,
    DateTime? CompletedAtUtc,
    IReadOnlyList<TaskSummaryResponse> Subtasks,
    DateTime CreatedAtUtc,
    UserRef? CreatedBy,
    UserRef? UpdatedBy,
    TaskPermissions Permissions);

/// <summary>タスク一覧の絞り込み・並べ替え・ページ（#41 の Query）。</summary>
public sealed class TaskListQuery
{
    /// <summary>ステータス（複数指定は OR）。</summary>
    public IReadOnlyList<Guid>? StatusId { get; set; }

    /// <summary>ステータス区分。既定は未着手と進行中（未完了のみ）。</summary>
    public IReadOnlyList<StatusCategory>? Category { get; set; }

    /// <summary>完了・中止も含める。</summary>
    public bool? IncludeClosed { get; set; }

    /// <summary>完了区分のタスクは完了日時がこの日時以降のものだけを返す。</summary>
    public DateTime? CompletedSince { get; set; }

    public IReadOnlyList<Guid>? AssigneeUserId { get; set; }

    /// <summary>担当者なし（assigneeUserId と OR）。</summary>
    public bool? Unassigned { get; set; }

    public bool? AssignedToMe { get; set; }

    public IReadOnlyList<TaskPriority>? Priority { get; set; }

    /// <summary>いずれかのタグを持つ。</summary>
    public IReadOnlyList<Guid>? TagId { get; set; }

    public Guid? MilestoneId { get; set; }

    public bool? NoMilestone { get; set; }

    public DateOnly? DueFrom { get; set; }

    public DateOnly? DueTo { get; set; }

    /// <summary>期限切れのみ（利用者のタイムゾーン基準）。</summary>
    public bool? Overdue { get; set; }

    [Range(1, 999999999)]
    public int? ParentTaskNumber { get; set; }

    public bool? TopLevelOnly { get; set; }

    /// <summary>タイトルの部分一致。</summary>
    [StringLength(100)]
    public string? Q { get; set; }

    /// <summary>
    /// 並べ替え: -updatedAt（既定）, updatedAt, number, -number, dueDate, -dueDate, priority, -priority, status, title, assignee, -createdAt
    /// </summary>
    public string? Sort { get; set; }

    [Range(1, int.MaxValue)]
    public int? Page { get; set; }

    [Range(1, 100)]
    public int? PageSize { get; set; }
}

public sealed class CreateTaskRequest
{
    [Display(Name = "タイトル")]
    [Required(ErrorMessage = ValidationMessages.Required)]
    [StringLength(200, MinimumLength = 1, ErrorMessage = ValidationMessages.LengthRange)]
    public string Title { get; set; } = string.Empty;

    [Display(Name = "説明")]
    [StringLength(20000, ErrorMessage = ValidationMessages.MaxLength)]
    public string? Description { get; set; }

    [Display(Name = "ステータス")]
    public Guid? StatusId { get; set; }

    [Display(Name = "優先度")]
    public TaskPriority? Priority { get; set; }

    [Display(Name = "担当者")]
    public Guid? AssigneeUserId { get; set; }

    [Display(Name = "開始日")]
    public DateOnly? StartDate { get; set; }

    [Display(Name = "期限")]
    public DateOnly? DueDate { get; set; }

    [Display(Name = "タグ")]
    [MaxLength(10, ErrorMessage = ValidationMessages.MaxItems)]
    public IReadOnlyList<Guid>? TagIds { get; set; }

    [Display(Name = "マイルストーン")]
    public Guid? MilestoneId { get; set; }

    [Display(Name = "親タスク")]
    [Range(1, 999999999, ErrorMessage = ValidationMessages.Range)]
    public int? ParentTaskNumber { get; set; }
}

/// <summary>タスク更新（#44）。送ったプロパティだけを変更し、null は値の消去を表す。</summary>
public sealed class UpdateTaskRequest : PatchRequest
{
    private string? _title;
    private string? _description;
    private Guid? _statusId;
    private TaskPriority? _priority;
    private Guid? _assigneeUserId;
    private DateOnly? _startDate;
    private DateOnly? _dueDate;
    private IReadOnlyList<Guid>? _tagIds;
    private Guid? _milestoneId;
    private int? _parentTaskNumber;

    [Display(Name = "タイトル")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = ValidationMessages.LengthRange)]
    public string? Title { get => _title; set => _title = Set(value); }

    [Display(Name = "説明")]
    [StringLength(20000, ErrorMessage = ValidationMessages.MaxLength)]
    public string? Description { get => _description; set => _description = Set(value); }

    [Display(Name = "ステータス")]
    public Guid? StatusId { get => _statusId; set => _statusId = Set(value); }

    [Display(Name = "優先度")]
    public TaskPriority? Priority { get => _priority; set => _priority = Set(value); }

    [Display(Name = "担当者")]
    public Guid? AssigneeUserId { get => _assigneeUserId; set => _assigneeUserId = Set(value); }

    [Display(Name = "開始日")]
    public DateOnly? StartDate { get => _startDate; set => _startDate = Set(value); }

    [Display(Name = "期限")]
    public DateOnly? DueDate { get => _dueDate; set => _dueDate = Set(value); }

    /// <summary>送った配列でタグを置き換える。</summary>
    [Display(Name = "タグ")]
    [MaxLength(10, ErrorMessage = ValidationMessages.MaxItems)]
    public IReadOnlyList<Guid>? TagIds { get => _tagIds; set => _tagIds = Set(value); }

    [Display(Name = "マイルストーン")]
    public Guid? MilestoneId { get => _milestoneId; set => _milestoneId = Set(value); }

    /// <summary>null で親子関係を解除する。</summary>
    [Display(Name = "親タスク")]
    [Range(1, 999999999, ErrorMessage = ValidationMessages.Range)]
    public int? ParentTaskNumber { get => _parentTaskNumber; set => _parentTaskNumber = Set(value); }

    /// <summary>取得時の version（楽観的排他）。</summary>
    [Display(Name = "版")]
    [JsonRequired]
    public uint Version { get; set; }
}

public sealed record TaskHistoryEntryResponse(
    Guid Id, TaskHistoryKind Kind, UserRef? Actor, string? OldValue, string? NewValue, DateTime OccurredAtUtc);

public sealed record TaskCommentResponse(
    Guid Id, UserRef Author, string Body, DateTime CreatedAtUtc, DateTime? EditedAtUtc, bool CanEdit, bool CanDelete);

public sealed class CreateTaskCommentRequest
{
    [Display(Name = "コメント")]
    [Required(ErrorMessage = ValidationMessages.Required)]
    [StringLength(10000, MinimumLength = 1, ErrorMessage = ValidationMessages.LengthRange)]
    public string Body { get; set; } = string.Empty;
}

public sealed class UpdateTaskCommentRequest
{
    [Display(Name = "コメント")]
    [Required(ErrorMessage = ValidationMessages.Required)]
    [StringLength(10000, MinimumLength = 1, ErrorMessage = ValidationMessages.LengthRange)]
    public string Body { get; set; } = string.Empty;
}

public sealed record TaskAttachmentResponse(
    Guid Id, string FileName, string ContentType, long SizeBytes, UserRef UploadedBy, DateTime UploadedAtUtc, bool CanDelete);

/// <summary>ダウンロード用の内容（Api がヘッダーを付けて返す）。</summary>
public sealed record AttachmentContent(Stream Content, string FileName, string ContentType, long SizeBytes);

public sealed record TaskSummaryWithProjectResponse(
    ProjectRef Project,
    string Key,
    int Number,
    string Title,
    StatusRef Status,
    TaskPriority Priority,
    UserRef? Assignee,
    DateOnly? StartDate,
    DateOnly? DueDate,
    bool IsOverdue,
    MilestoneRef? Milestone,
    IReadOnlyList<TagRef> Tags,
    TaskRef? Parent,
    int SubtaskCount,
    int ClosedSubtaskCount,
    int CommentCount,
    int AttachmentCount,
    DateTime UpdatedAtUtc,
    uint Version);

public sealed record MyTaskBuckets(
    IReadOnlyList<TaskSummaryWithProjectResponse> Overdue,
    IReadOnlyList<TaskSummaryWithProjectResponse> Today,
    IReadOnlyList<TaskSummaryWithProjectResponse> ThisWeek,
    IReadOnlyList<TaskSummaryWithProjectResponse> Later,
    IReadOnlyList<TaskSummaryWithProjectResponse> NoDueDate);

public sealed record MyTasksResponse(
    DateOnly Today,
    string TimeZoneId,
    MyTaskBuckets Buckets,
    IReadOnlyList<TaskSummaryWithProjectResponse> RecentlyCompleted,
    bool Truncated);

public sealed record TaskSearchResultResponse(
    string Key, string Title, ProjectRef Project, StatusRef Status, UserRef? Assignee);
