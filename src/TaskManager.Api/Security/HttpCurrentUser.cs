using TaskManager.Application.Abstractions;

namespace TaskManager.Api.Security;

/// <summary>JWT のクレームから現在の利用者を返す。</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private HttpContext? Context => accessor.HttpContext;

    public bool IsAuthenticated => Context?.User.Identity?.IsAuthenticated == true && TryGetUserId(out _);

    public Guid UserId => TryGetUserId(out var id)
        ? id
        : throw new InvalidOperationException("ログインしていない状態で利用者 ID を参照しました。");

    public bool IsSystemAdministrator => Context?.User.HasClaim(TokenClaims.Role, TokenClaims.AdministratorRole) == true;

    public Guid? SessionFamilyId =>
        Guid.TryParse(Context?.User.FindFirst(TokenClaims.SessionId)?.Value, out var sid) ? sid : null;

    public string? IpAddress => Context?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent
    {
        get
        {
            var ua = Context?.Request.Headers.UserAgent.ToString();
            return string.IsNullOrEmpty(ua) ? null : ua;
        }
    }

    private bool TryGetUserId(out Guid id)
    {
        id = Guid.Empty;
        return Context?.User.FindFirst(TokenClaims.Subject)?.Value is { } sub && Guid.TryParse(sub, out id);
    }
}
