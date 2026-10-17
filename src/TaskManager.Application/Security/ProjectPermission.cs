using TaskManager.Domain.Common;

namespace TaskManager.Application.Security;

/// <summary>プロジェクト内の操作単位の権限（docs/design/authentication-authorization.md §6.2）。</summary>
public enum ProjectPermission
{
    ViewProject,
    EditProjectSettings,
    ArchiveProject,
    ManageMembers,
    ManageStatuses,
    ManageTags,
    ManageMilestones,
    CreateTasks,
    EditTasks,
    DeleteOwnTask,
    DeleteAnyTask,
    Comment,
    DeleteAnyComment,
    UploadAttachments,
    DeleteAnyAttachment,
    LeaveProject,
    DeleteProject,
}

public static class ProjectPermissionRules
{
    /// <summary>ロールが権限を持つか（システム管理者の特例は ProjectAccessGuard で扱う）。</summary>
    public static bool IsGranted(ProjectRole role, ProjectPermission permission) => permission switch
    {
        ProjectPermission.ViewProject or ProjectPermission.LeaveProject => true,
        ProjectPermission.CreateTasks or ProjectPermission.EditTasks or ProjectPermission.DeleteOwnTask
            or ProjectPermission.Comment or ProjectPermission.UploadAttachments
            or ProjectPermission.ManageTags or ProjectPermission.ManageMilestones => role >= ProjectRole.Editor,
        ProjectPermission.DeleteAnyTask or ProjectPermission.DeleteAnyComment or ProjectPermission.DeleteAnyAttachment
            or ProjectPermission.ManageStatuses or ProjectPermission.EditProjectSettings
            or ProjectPermission.ArchiveProject or ProjectPermission.ManageMembers => role == ProjectRole.Owner,
        ProjectPermission.DeleteProject => false,
        _ => false,
    };

    /// <summary>アーカイブ中でも許可される操作か（authentication-authorization.md §6.2 末尾）。</summary>
    public static bool IsAllowedWhileArchived(ProjectPermission permission) =>
        permission is ProjectPermission.ViewProject or ProjectPermission.ArchiveProject or ProjectPermission.DeleteProject;
}
