using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using TaskManager.Api.Errors;
using TaskManager.Application.Common;

namespace TaskManager.Api.Security;

/// <summary>レート制限（security-design.md S-13）。</summary>
public static class RateLimitPolicies
{
    public const string Auth = "auth";
    public const string Refresh = "refresh";
    public const string Upload = "upload";
    public const string Search = "search";
    public const string PasswordChange = "password-change";

    public static IServiceCollection AddTaskManagerRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(Auth, context => Fixed(IpKey(context), 10));
            options.AddPolicy(Refresh, context => Fixed(IpKey(context), 30));
            options.AddPolicy(Upload, context => Fixed(UserKey(context), 20));
            options.AddPolicy(Search, context => Fixed(UserKey(context), 60));
            // 盗まれたアクセストークンで現在のパスワードを総当たりされないよう、利用者ごとに 5 回/分（Phase 7）
            options.AddPolicy(PasswordChange, context => Fixed(UserKey(context), 5));
            // 全 API 共通: 利用者（未ログインは IP）あたり 300 回/分
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
                    ? Fixed(UserKey(context), 300)
                    : RateLimitPartition.GetNoLimiter("none"));
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                await ProblemResults.WriteAsync(context.HttpContext, StatusCodes.Status429TooManyRequests,
                    ErrorCodes.RateLimitExceeded, "短時間にリクエストが集中しました。", "しばらく時間をおいてから再度お試しください。");
            };
        });
        return services;
    }

    private static RateLimitPartition<string> Fixed(string key, int permitPerMinute) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });

    private static string IpKey(HttpContext context) => "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");

    private static string UserKey(HttpContext context) =>
        context.User.FindFirst(TokenClaims.Subject)?.Value is { } sub ? "user:" + sub : IpKey(context);
}
