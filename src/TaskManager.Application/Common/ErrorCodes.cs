using TaskManager.Domain.Common;

namespace TaskManager.Application.Common;

/// <summary>エラーコードと日本語の文言（docs/design/api-design.md §1.5）。</summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "validation.failed";
    public const string RequestMalformed = "request.malformed";
    public const string InvalidSort = "request.invalid_sort";
    public const string InvalidCredentials = "auth.invalid_credentials";
    public const string Locked = "auth.locked";
    public const string Unauthenticated = "auth.unauthenticated";
    public const string SessionExpired = "auth.session_expired";
    public const string PasswordChangeRequired = "auth.password_change_required";
    public const string CurrentPasswordInvalid = "auth.current_password_invalid";
    public const string CsrfRejected = "auth.csrf_rejected";
    public const string Forbidden = "access.forbidden";
    public const string NotFound = "resource.not_found";
    public const string ProjectArchived = "project.archived";
    public const string ProjectKeyTaken = "project.key_taken";
    public const string ProjectNameTaken = "project.name_taken";
    public const string ProjectNotArchived = "project.not_archived";
    public const string MemberAlreadyExists = "member.already_exists";
    public const string MemberUserInactive = "member.user_inactive";
    public const string MemberLastOwner = "member.last_owner";
    public const string StatusNameTaken = "status.name_taken";
    public const string StatusLimitReached = "status.limit_reached";
    public const string StatusIsDefault = "status.is_default";
    public const string StatusInUse = "status.in_use";
    public const string StatusLastOne = "status.last_one";
    public const string TagNameTaken = "tag.name_taken";
    public const string TagLimitReached = "tag.limit_reached";
    public const string MilestoneNameTaken = "milestone.name_taken";
    public const string MilestoneClosed = "milestone.closed";
    public const string TaskHasSubtasks = "task.has_subtasks";
    public const string AttachmentTooLarge = "attachment.too_large";
    public const string RequestTooLarge = "request.too_large";
    public const string AttachmentTypeNotAllowed = "attachment.type_not_allowed";
    public const string AttachmentLimitReached = "attachment.limit_reached";
    public const string UserEmailTaken = "user.email_taken";
    public const string UserLastAdministrator = "user.last_administrator";
    public const string UserCannotDeactivateSelf = "user.cannot_deactivate_self";
    public const string ConcurrencyConflict = "concurrency.conflict";
    public const string RateLimitExceeded = "rate_limit.exceeded";
    public const string ServerError = "server.error";
}

/// <summary>よく使う例外の生成。</summary>
public static class Errors
{
    public static AppException NotFound() =>
        new(AppErrorKind.NotFound, ErrorCodes.NotFound, "対象が見つかりません。");

    public static AppException Forbidden() =>
        new(AppErrorKind.Forbidden, ErrorCodes.Forbidden, "この操作を行う権限がありません。");

    public static AppException ProjectArchived() =>
        new(AppErrorKind.Conflict, ErrorCodes.ProjectArchived, "アーカイブされたプロジェクトは変更できません。",
            "変更するには、プロジェクトの管理者がアーカイブを解除してください。");

    public static AppException Conflict(string code, string title, string? detail = null) =>
        new(AppErrorKind.Conflict, code, title, detail);

    public static AppException ConcurrencyConflict() =>
        new(AppErrorKind.Conflict, ErrorCodes.ConcurrencyConflict, "他の人が先に更新しました。",
            "最新の内容を読み込んでから、もう一度操作してください。");

    /// <summary>入力項目 1 つの検証エラー。</summary>
    public static AppException Validation(string field, string message) =>
        new(AppErrorKind.BadRequest, ErrorCodes.ValidationFailed, "入力内容に誤りがあります。",
            "赤く表示された項目を確認してください。",
            new Dictionary<string, string[]> { [field] = [message] });

    public static AppException FromDomain(DomainException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception.Field is null
            ? new AppException(AppErrorKind.BadRequest, exception.Code, exception.Message)
            : Validation(exception.Field, exception.Message);
    }
}
