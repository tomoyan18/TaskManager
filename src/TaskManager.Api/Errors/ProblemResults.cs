using Microsoft.AspNetCore.Mvc;

namespace TaskManager.Api.Errors;

/// <summary>Problem Details（RFC 9457）の生成（api-design.md §1.4）。</summary>
public static class ProblemResults
{
    public const string ContentType = "application/problem+json";

    public static ProblemDetails Build(
        HttpContext context, int status, string code, string title, string? detail = null,
        IReadOnlyDictionary<string, string[]>? errors = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var problem = new ProblemDetails
        {
            Type = $"urn:taskmanager:problem:{code}",
            Title = title,
            Status = status,
            Detail = detail,
            Instance = context.Request.Path,
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = context.TraceIdentifier;
        if (errors is { Count: > 0 })
        {
            problem.Extensions["errors"] = errors;
        }

        return problem;
    }

    /// <summary>
    /// フィルター・モデル検証から返す Problem Details。ObjectResult にすると全体の [Produces("application/json")] で
    /// Content-Type が上書きされるため、応答を直接書き込む結果にする（Phase 6 で検出）。
    /// </summary>
    public static IActionResult Create(
        HttpContext context, int status, string code, string title, string? detail = null,
        IReadOnlyDictionary<string, string[]>? errors = null) =>
        new ProblemActionResult(status, code, title, detail, errors);

    private sealed class ProblemActionResult(
        int status, string code, string title, string? detail, IReadOnlyDictionary<string, string[]>? errors) : IActionResult
    {
        public Task ExecuteResultAsync(ActionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return WriteAsync(context.HttpContext, status, code, title, detail, errors);
        }
    }

    public static Task WriteAsync(
        HttpContext context, int status, string code, string title, string? detail = null,
        IReadOnlyDictionary<string, string[]>? errors = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Response.StatusCode = status;
        context.Response.ContentType = ContentType;
        context.Response.Headers.CacheControl = "no-store";
        return context.Response.WriteAsJsonAsync(
            Build(context, status, code, title, detail, errors), (System.Text.Json.JsonSerializerOptions?)null, ContentType);
    }
}
