namespace TaskManager.Api.Configuration;

/// <summary>リフレッシュ Cookie の設定（authentication-authorization.md §2.2）。</summary>
public sealed class AuthCookieOptions
{
    public const string SectionName = "AuthCookie";
    public const string CookieName = "tm_refresh";
    public const string CookiePath = "/api/v1/auth";

    /// <summary>Secure 属性（既定 true。HTTP の LAN 検証時のみ false。環境変数 AuthCookie__Secure / TM_COOKIE_SECURE）。</summary>
    public bool Secure { get; set; } = true;

    /// <summary>自オリジン以外に、refresh / logout を許可するオリジン（通常は空）。</summary>
    public IReadOnlyList<string> AllowedOrigins { get; set; } = [];
}
