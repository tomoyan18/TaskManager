using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaskManager.Api.Http;
using TaskManager.Api.Security;
using TaskManager.Application.Auth;

namespace TaskManager.Api.Controllers;

[Route("api/v1/auth")]
[Tags("Auth")]
public sealed class AuthController(AuthService authService, RefreshCookie refreshCookie, SecurityStampValidator stampValidator)
    : ApiControllerBase
{
    /// <summary>メールアドレスとパスワードでログインする。成功するとアクセストークンを返し、リフレッシュ Cookie を設定する。</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [EndpointName("Login")]
    [EndpointSummary("ログインする")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, cancellationToken);
        refreshCookie.Write(Response, result.RefreshToken, result.RefreshExpiresAtUtc);
        return Ok(result.Response);
    }

    /// <summary>リフレッシュ Cookie で新しいアクセストークンを取得する（Cookie も更新される）。</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [RequireSameOrigin]
    [AllowWhenPasswordChangeRequired]
    [EnableRateLimiting(RateLimitPolicies.Refresh)]
    [EndpointName("RefreshAccessToken")]
    [EndpointSummary("アクセストークンを更新する")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    public async Task<ActionResult<LoginResponse>> Refresh(CancellationToken cancellationToken)
    {
        try
        {
            var result = await authService.RefreshAsync(refreshCookie.Read(Request), cancellationToken);
            refreshCookie.Write(Response, result.RefreshToken, result.RefreshExpiresAtUtc);
            return Ok(result.Response);
        }
        catch (Application.Common.AppException)
        {
            refreshCookie.Clear(Response);
            throw;
        }
    }

    /// <summary>ログアウトする（現在のログインセッションを無効にし、Cookie を削除する）。</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [RequireSameOrigin]
    [AllowWhenPasswordChangeRequired]
    [EnableRateLimiting(RateLimitPolicies.Refresh)]
    [EndpointName("Logout")]
    [EndpointSummary("ログアウトする")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(refreshCookie.Read(Request), cancellationToken);
        refreshCookie.Clear(Response);
        return NoContent();
    }

    /// <summary>自分のパスワードを変更する。他の端末のログインは無効になり、この端末には新しいトークンを発行する。</summary>
    [HttpPost("password")]
    [AllowWhenPasswordChangeRequired]
    [EnableRateLimiting(RateLimitPolicies.PasswordChange)]
    [EndpointName("ChangePassword")]
    [EndpointSummary("パスワードを変更する")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    public async Task<ActionResult<LoginResponse>> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.ChangePasswordAsync(request, cancellationToken);
        stampValidator.Invalidate(result.Response.User.Id);
        refreshCookie.Write(Response, result.RefreshToken, result.RefreshExpiresAtUtc);
        return Ok(result.Response);
    }
}
