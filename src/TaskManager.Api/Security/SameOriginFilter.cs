using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using TaskManager.Api.Configuration;
using TaskManager.Api.Errors;
using TaskManager.Application.Common;

namespace TaskManager.Api.Security;

/// <summary>
/// refresh / logout の CSRF 対策（authentication-authorization.md §2.2）。
/// Origin が自オリジン（または設定した許可オリジン）と一致すること。Origin がない場合は Sec-Fetch-Site: same-origin を要求する。
/// </summary>
public sealed class SameOriginFilter(IOptions<AuthCookieOptions> options) : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (!context.ActionDescriptor.EndpointMetadata.OfType<RequireSameOriginAttribute>().Any())
        {
            return;
        }

        var request = context.HttpContext.Request;
        var origin = request.Headers.Origin.ToString();
        var self = $"{request.Scheme}://{request.Host}";
        var allowed = string.IsNullOrEmpty(origin)
            ? string.Equals(request.Headers["Sec-Fetch-Site"].ToString(), "same-origin", StringComparison.Ordinal)
            : string.Equals(origin, self, StringComparison.OrdinalIgnoreCase)
              || options.Value.AllowedOrigins.Any(o => string.Equals(o.TrimEnd('/'), origin, StringComparison.OrdinalIgnoreCase));
        if (!allowed)
        {
            context.Result = ProblemResults.Create(context.HttpContext, StatusCodes.Status403Forbidden,
                ErrorCodes.CsrfRejected, "不正なリクエストのため処理できませんでした。");
        }
    }
}
