using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Common;

namespace TaskManager.Api.Errors;

/// <summary>
/// 例外を Problem Details に変換する（architecture.md §4）。500 では内部情報（例外メッセージ・スタックトレース・SQL）を出さない。
/// </summary>
public sealed partial class TaskManagerExceptionHandler(ILogger<TaskManagerExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        switch (exception)
        {
            case AppException app:
                await ProblemResults.WriteAsync(httpContext, ToStatus(app.Kind), app.Code, app.Title, app.Detail, app.Errors);
                return true;
            case DbUpdateConcurrencyException:
                await ProblemResults.WriteAsync(httpContext, StatusCodes.Status409Conflict, ErrorCodes.ConcurrencyConflict,
                    "他の人が先に更新しました。", "最新の内容を読み込んでから、もう一度操作してください。");
                return true;
            case DbUpdateException db when IsDuplicateKey(db):
                await ProblemResults.WriteAsync(httpContext, StatusCodes.Status409Conflict, ErrorCodes.ConcurrencyConflict,
                    "他の人が先に更新しました。", "最新の内容を読み込んでから、もう一度操作してください。");
                return true;
            case BadHttpRequestException bad when bad.StatusCode == StatusCodes.Status413PayloadTooLarge:
                // 添付以外（JSON の 1MB 上限など）は request.too_large にする（Phase 7）
                await ProblemResults.WriteAsync(httpContext, StatusCodes.Status413PayloadTooLarge,
                    httpContext.Request.Path.Value?.EndsWith("/attachments", StringComparison.OrdinalIgnoreCase) == true
                        ? ErrorCodes.AttachmentTooLarge : ErrorCodes.RequestTooLarge,
                    "送信できるデータの上限を超えています。");
                return true;
            case BadHttpRequestException:
                await ProblemResults.WriteAsync(httpContext, StatusCodes.Status400BadRequest, ErrorCodes.RequestMalformed,
                    "リクエストの形式が正しくありません。");
                return true;
            case OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested:
                return true;
            default:
                LogUnhandled(logger, exception, httpContext.TraceIdentifier);
                await ProblemResults.WriteAsync(httpContext, StatusCodes.Status500InternalServerError, ErrorCodes.ServerError,
                    "処理中にエラーが発生しました。", "時間をおいて再度お試しください。問題が続く場合は管理者に連絡してください。");
                return true;
        }
    }

    public static int ToStatus(AppErrorKind kind) => kind switch
    {
        AppErrorKind.BadRequest => StatusCodes.Status400BadRequest,
        AppErrorKind.Unauthenticated => StatusCodes.Status401Unauthorized,
        AppErrorKind.PasswordChangeRequired or AppErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        AppErrorKind.NotFound => StatusCodes.Status404NotFound,
        AppErrorKind.Conflict => StatusCodes.Status409Conflict,
        AppErrorKind.PayloadTooLarge => StatusCodes.Status413PayloadTooLarge,
        AppErrorKind.UnsupportedMediaType => StatusCodes.Status415UnsupportedMediaType,
        _ => StatusCodes.Status500InternalServerError,
    };

    /// <summary>MySQL の一意制約違反（エラー 1062）。同時更新の競合として扱う。</summary>
    private static bool IsDuplicateKey(DbUpdateException exception)
    {
        for (Exception? e = exception; e is not null; e = e.InnerException)
        {
            if (e.GetType().Name == "MySqlException" && e.GetType().GetProperty("Number")?.GetValue(e) is int number && number == 1062)
            {
                return true;
            }
        }

        return false;
    }

    [LoggerMessage(EventId = 5000, Level = LogLevel.Error, Message = "Unhandled exception. traceId={TraceId}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string traceId);
}
