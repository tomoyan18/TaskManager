using Microsoft.AspNetCore.Mvc.Filters;
using TaskManager.Api.Errors;
using TaskManager.Application.Common;

namespace TaskManager.Api.Security;

/// <summary>REQ-AUTH-05: 一時パスワード状態の利用者は、許可された操作以外を 403 にする。</summary>
public sealed class PasswordChangeRequiredFilter : IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        // 匿名で呼べる API（ログイン・システム情報）はトークンの有無に関係なく同じ応答にする（Phase 6 で仕様を明確化）
        var metadata = context.ActionDescriptor.EndpointMetadata;
        var exempt = metadata.OfType<AllowWhenPasswordChangeRequiredAttribute>().Any()
            || metadata.OfType<Microsoft.AspNetCore.Authorization.IAllowAnonymous>().Any();
        if (!exempt && user.HasClaim(TokenClaims.PasswordChangeRequired, "1"))
        {
            context.Result = ProblemResults.Create(context.HttpContext, StatusCodes.Status403Forbidden,
                ErrorCodes.PasswordChangeRequired, "パスワードの変更が必要です。", "最初に、ご自身で新しいパスワードを設定してください。");
        }

        return Task.CompletedTask;
    }
}
