namespace TaskManager.Application.Common;

/// <summary>HTTP の状態に対応するエラーの種類。Api 層でステータスコードに変換する。</summary>
public enum AppErrorKind
{
    BadRequest,
    Unauthenticated,
    PasswordChangeRequired,
    Forbidden,
    NotFound,
    Conflict,
    PayloadTooLarge,
    UnsupportedMediaType,
}

/// <summary>
/// Application 層のエラー。Code・Title・Detail は docs/design/api-design.md §1.5 に対応し、
/// Problem Details としてそのまま利用者に表示してよい日本語にする。
/// </summary>
public class AppException : Exception
{
    public AppException(AppErrorKind kind, string code, string title, string? detail = null,
        IReadOnlyDictionary<string, string[]>? errors = null)
        : base(title)
    {
        Kind = kind;
        Code = code;
        Title = title;
        Detail = detail;
        Errors = errors;
    }

    public AppException()
        : this(AppErrorKind.BadRequest, ErrorCodes.ValidationFailed, "入力内容に誤りがあります。")
    {
    }

    public AppException(string message)
        : this(AppErrorKind.BadRequest, ErrorCodes.ValidationFailed, message)
    {
    }

    public AppException(string message, Exception innerException)
        : base(message, innerException)
    {
        Kind = AppErrorKind.BadRequest;
        Code = ErrorCodes.ValidationFailed;
        Title = message;
    }

    public AppErrorKind Kind { get; }

    public string Code { get; }

    public string Title { get; }

    public string? Detail { get; }

    public IReadOnlyDictionary<string, string[]>? Errors { get; }
}
