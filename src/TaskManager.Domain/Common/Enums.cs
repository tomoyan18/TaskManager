namespace TaskManager.Domain.Common;

// 列挙の値は DB に TINYINT で保存する（docs/design/domain-model.md §3）。値を変更しないこと。

public enum SystemRole : byte
{
    Member = 1,
    Administrator = 2,
}

public enum ProjectRole : byte
{
    Viewer = 1,
    Editor = 2,
    Owner = 3,
}

public enum StatusCategory : byte
{
    NotStarted = 1,
    InProgress = 2,
    Done = 3,
    Canceled = 4,
}

public enum StatusColor : byte
{
    Gray = 1,
    Blue = 2,
    Teal = 3,
    Green = 4,
    Yellow = 5,
    Orange = 6,
    Red = 7,
    Purple = 8,
    Pink = 9,
}

public enum TaskPriority : byte
{
    Low = 1,
    Normal = 2,
    High = 3,
    Urgent = 4,
}

public enum MilestoneState : byte
{
    Open = 1,
    Closed = 2,
}

public enum TaskHistoryKind : byte
{
    Created = 1,
    TitleChanged = 2,
    DescriptionChanged = 3,
    StatusChanged = 4,
    PriorityChanged = 5,
    AssigneeChanged = 6,
    StartDateChanged = 7,
    DueDateChanged = 8,
    MilestoneChanged = 9,
    ParentChanged = 10,
    TagsChanged = 11,
    AttachmentAdded = 12,
    AttachmentRemoved = 13,
}

public enum NoticeKind : byte
{
    TaskAssigned = 1,
    TaskCommented = 2,
    TaskCompleted = 3,
    AddedToProject = 4,
}

public enum SecurityEventKind : byte
{
    LoginSucceeded = 1,
    LoginFailed = 2,
    AccountLocked = 3,
    LoggedOut = 4,
    PasswordChanged = 5,
    PasswordReset = 6,
    RefreshTokenReuseDetected = 7,
    UserCreated = 8,
    UserUpdated = 9,
    UserDeactivated = 10,
    UserActivated = 11,
    UserUnlocked = 12,
    MembershipAdded = 13,
    MembershipRoleChanged = 14,
    MembershipRemoved = 15,
    ProjectDeleted = 16,
}

public static class StatusCategoryExtensions
{
    /// <summary>完了または中止（＝終了）か。</summary>
    public static bool IsClosed(this StatusCategory category) =>
        category is StatusCategory.Done or StatusCategory.Canceled;
}

public static class ProjectRoleExtensions
{
    /// <summary>担当者になれるロールか（domain-model.md M-03）。</summary>
    public static bool CanBeAssigned(this ProjectRole role) => role >= ProjectRole.Editor;
}
