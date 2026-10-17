using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TaskManager.Api.Configuration;
using TaskManager.Application.Abstractions;
using TaskManager.Domain.Accounts;
using TaskManager.Domain.Common;

namespace TaskManager.Api.Security;

/// <summary>
/// アクセストークン（JWT, HS256, 15 分）の発行。検証と同じ JwtOptions を使うため Api 層に置く
/// （設計の Infrastructure 配置からの変更。docs/implementation/status.md に記録）。
/// </summary>
public sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> options, TimeProvider timeProvider) : IAccessTokenIssuer
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken Issue(UserAccount user, Guid sessionFamilyId)
    {
        ArgumentNullException.ThrowIfNull(user);
        var jwt = options.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var claims = new List<Claim>
        {
            new(TokenClaims.Subject, user.Id.ToString()),
            new(TokenClaims.Name, user.DisplayName),
            new(TokenClaims.Role, user.SystemRole == SystemRole.Administrator ? TokenClaims.AdministratorRole : TokenClaims.MemberRole),
            new(TokenClaims.SecurityStamp, user.SecurityStamp.ToString()),
            new(TokenClaims.SessionId, sessionFamilyId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        if (user.MustChangePassword)
        {
            claims.Add(new Claim(TokenClaims.PasswordChangeRequired, "1"));
        }

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            NotBefore = now,
            Expires = now.Add(Lifetime),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(jwt.GetSigningKeyBytes()), SecurityAlgorithms.HmacSha256),
        });
        return new AccessToken(token, (int)Lifetime.TotalSeconds);
    }
}
