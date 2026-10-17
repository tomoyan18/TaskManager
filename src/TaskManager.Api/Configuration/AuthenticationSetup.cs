using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using TaskManager.Api.Errors;
using TaskManager.Api.Security;
using TaskManager.Application.Common;

namespace TaskManager.Api.Configuration;

/// <summary>認証・認可の設定（authentication-authorization.md §2、§7.1）。</summary>
public static class AuthenticationSetup
{
    public static IServiceCollection AddTaskManagerAuthentication(
        this IServiceCollection services, IConfiguration configuration, bool isDocumentGeneration)
    {
        var section = configuration.GetSection(JwtOptions.SectionName);
        var jwtOptions = section.Get<JwtOptions>() ?? new JwtOptions();

        // ビルド時の OpenAPI 文書生成では本番設定なしで起動するため、鍵の検証を省略する
        var signingKey = isDocumentGeneration
            ? new byte[JwtOptions.MinimumSigningKeyBytes]
            : jwtOptions.GetSigningKeyBytes();

        services.AddOptions<JwtOptions>().Bind(section).ValidateDataAnnotations();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(signingKey),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = TokenClaims.Name,
                    RoleClaimType = TokenClaims.Role,
                };
                options.Events = new JwtBearerEvents
                {
                    // パスワード変更・利用停止を即時に反映する（security_stamp の照合）
                    OnTokenValidated = async context =>
                    {
                        var principal = context.Principal;
                        var validator = context.HttpContext.RequestServices.GetRequiredService<SecurityStampValidator>();
                        if (principal?.FindFirst(TokenClaims.Subject)?.Value is not { } sub || !Guid.TryParse(sub, out var userId)
                            || !await validator.IsValidAsync(userId, principal.FindFirst(TokenClaims.SecurityStamp)?.Value))
                        {
                            context.Fail("security stamp mismatch");
                        }
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        await ProblemResults.WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized,
                            ErrorCodes.Unauthenticated, "ログインが必要です。");
                    },
                    OnForbidden = context => ProblemResults.WriteAsync(context.HttpContext, StatusCodes.Status403Forbidden,
                        ErrorCodes.Forbidden, "この操作を行う権限がありません。"),
                };
            });

        services.AddAuthorizationBuilder()
            // 既定でログイン必須。匿名で呼べるのは [AllowAnonymous] を付けたものだけ（security-design.md S-21）
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.SystemAdministrator, policy => policy.RequireAuthenticatedUser()
                .RequireClaim(TokenClaims.Role, TokenClaims.AdministratorRole));

        return services;
    }
}
