namespace TaskManager.Api.Security;

/// <summary>アクセストークンのクレーム名（authentication-authorization.md §2.1）。</summary>
public static class TokenClaims
{
    public const string Subject = "sub";
    public const string Name = "name";
    public const string Role = "role";
    public const string SecurityStamp = "stamp";
    public const string PasswordChangeRequired = "pwchg";
    public const string SessionId = "sid";
    public const string AdministratorRole = "administrator";
    public const string MemberRole = "member";
}
