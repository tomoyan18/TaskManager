using TaskManager.Application.Common;

namespace TaskManager.Testing;

/// <summary>Application 層のエラー（AppException）の検証。</summary>
public static class AppAssert
{
    public static async Task<AppException> ThrowsAsync(AppErrorKind kind, string code, Func<Task> action)
    {
        var ex = await Assert.ThrowsAsync<AppException>(action);
        Assert.Equal(kind, ex.Kind);
        Assert.Equal(code, ex.Code);
        return ex;
    }

    public static Task<AppException> NotFoundAsync(Func<Task> action) =>
        ThrowsAsync(AppErrorKind.NotFound, ErrorCodes.NotFound, action);

    public static Task<AppException> ForbiddenAsync(Func<Task> action) =>
        ThrowsAsync(AppErrorKind.Forbidden, ErrorCodes.Forbidden, action);

    public static async Task<AppException> ValidationAsync(string field, Func<Task> action)
    {
        var ex = await ThrowsAsync(AppErrorKind.BadRequest, ErrorCodes.ValidationFailed, action);
        Assert.NotNull(ex.Errors);
        Assert.True(ex.Errors!.ContainsKey(field), $"項目 {field} のエラーがありません。実際: {string.Join(", ", ex.Errors.Keys)}");
        return ex;
    }
}
