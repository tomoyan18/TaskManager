namespace TaskManager.Api.Middleware;

/// <summary>
/// API 応答に共通のセキュリティヘッダーを付ける（docs/design/security-design.md §3）。
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var path = context.Request.Path;

        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            if (!path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase))
            {
                // Swagger UI 以外は API（JSON）のみを返すため、最も厳しい CSP にする
                headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            }

            if (path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            {
                headers.CacheControl = "no-store";
            }

            return Task.CompletedTask;
        });

        return next(context);
    }
}
