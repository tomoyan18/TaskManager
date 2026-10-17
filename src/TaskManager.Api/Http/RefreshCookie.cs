using Microsoft.Extensions.Options;
using TaskManager.Api.Configuration;

namespace TaskManager.Api.Http;

/// <summary>リフレッシュトークン Cookie（HttpOnly; SameSite=Strict; Path=/api/v1/auth）の読み書き。</summary>
public sealed class RefreshCookie(IOptions<AuthCookieOptions> options, TimeProvider timeProvider)
{
    public string? Read(HttpRequest request) => request.Cookies[AuthCookieOptions.CookieName];

    public void Write(HttpResponse response, string token, DateTime expiresAtUtc)
    {
        ArgumentNullException.ThrowIfNull(response);
        var maxAge = expiresAtUtc - timeProvider.GetUtcNow().UtcDateTime;
        response.Cookies.Append(AuthCookieOptions.CookieName, token, Build(maxAge > TimeSpan.Zero ? maxAge : TimeSpan.Zero));
    }

    public void Clear(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Cookies.Delete(AuthCookieOptions.CookieName, Build(null));
    }

    private CookieOptions Build(TimeSpan? maxAge) => new()
    {
        HttpOnly = true,
        Secure = options.Value.Secure,
        SameSite = SameSiteMode.Strict,
        Path = AuthCookieOptions.CookiePath,
        MaxAge = maxAge,
        IsEssential = true,
    };
}
