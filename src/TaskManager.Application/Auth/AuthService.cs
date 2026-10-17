using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Common;
using TaskManager.Application.Security;
using TaskManager.Application.Users;
using TaskManager.Domain.Accounts;
using TaskManager.Domain.Common;

namespace TaskManager.Application.Auth;

/// <summary>ログイン・トークン更新・ログアウト・パスワード変更（authentication-authorization.md §3）。</summary>
public sealed class AuthService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IPasswordHasher passwordHasher,
    IAccessTokenIssuer accessTokenIssuer,
    IRefreshTokenGenerator refreshTokenGenerator,
    SecurityAuditService audit,
    TimeProvider timeProvider)
{
    private static string? _dummyHash;

    private static AppException InvalidCredentials() =>
        new(AppErrorKind.Unauthenticated, ErrorCodes.InvalidCredentials, "メールアドレスまたはパスワードが正しくありません。");

    private static AppException SessionExpired() =>
        new(AppErrorKind.Unauthenticated, ErrorCodes.SessionExpired, "ログインの有効期限が切れました。もう一度ログインしてください。");

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.UtcNow();
        var email = EmailAddress.Normalize(request.Email);
        var user = await db.UserAccounts.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null)
        {
            // 存在しない利用者でもハッシュ検証を行い、応答時間の差で存在を推測されないようにする
            _dummyHash ??= passwordHasher.Hash("taskmanager-dummy-password-for-timing");
            passwordHasher.Verify(_dummyHash, request.Password);
            audit.Record(SecurityEventKind.LoginFailed, detail: new Dictionary<string, string?> { ["reason"] = "unknown_user" });
            await db.SaveChangesAsync(cancellationToken);
            throw InvalidCredentials();
        }

        if (!user.IsActive)
        {
            passwordHasher.Verify(user.PasswordHash, request.Password);
            audit.Record(SecurityEventKind.LoginFailed, user.Id, detail: new Dictionary<string, string?> { ["reason"] = "inactive" });
            await db.SaveChangesAsync(cancellationToken);
            throw InvalidCredentials();
        }

        if (user.IsLockedAt(now))
        {
            audit.Record(SecurityEventKind.LoginFailed, user.Id, detail: new Dictionary<string, string?> { ["reason"] = "locked" });
            await db.SaveChangesAsync(cancellationToken);
            throw new AppException(AppErrorKind.Unauthenticated, ErrorCodes.Locked,
                "ログインに続けて失敗したため、一時的にロックされています。",
                "15 分ほど待ってから再度お試しください。急ぎの場合はシステム管理者に解除を依頼してください。");
        }

        var result = passwordHasher.Verify(user.PasswordHash, request.Password);
        if (result == PasswordCheckResult.Failed)
        {
            var locked = user.RegisterFailedLogin(now);
            audit.Record(locked ? SecurityEventKind.AccountLocked : SecurityEventKind.LoginFailed, user.Id,
                detail: new Dictionary<string, string?> { ["reason"] = "wrong_password" });
            await db.SaveChangesAsync(cancellationToken);
            throw InvalidCredentials();
        }

        if (result == PasswordCheckResult.SuccessRehashNeeded)
        {
            user.Rehash(passwordHasher.Hash(request.Password));
        }

        user.RegisterSuccessfulLogin(now);
        var auth = await StartSessionAsync(user, now, cancellationToken);
        audit.Record(SecurityEventKind.LoginSucceeded, user.Id, actorUserId: user.Id);
        await db.SaveChangesAsync(cancellationToken);
        return auth;
    }

    public async Task<AuthResult> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw SessionExpired();
        }

        var now = timeProvider.UtcNow();
        var hash = refreshTokenGenerator.Hash(refreshToken);
        var session = await db.RefreshSessions.SingleOrDefaultAsync(s => s.TokenHash == hash, cancellationToken)
            ?? throw SessionExpired();

        if (session.IsRotated)
        {
            // R-02: 使用済みトークンの再提示 → 盗用の疑い。同じ系列をすべて失効する
            await RevokeFamilyAsync(session.FamilyId, now, cancellationToken);
            audit.Record(SecurityEventKind.RefreshTokenReuseDetected, session.UserId, actorUserId: session.UserId);
            await db.SaveChangesAsync(cancellationToken);
            throw SessionExpired();
        }

        if (!session.IsUsableAt(now))
        {
            throw SessionExpired();
        }

        var user = await db.UserAccounts.SingleOrDefaultAsync(u => u.Id == session.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            session.Revoke(now);
            await db.SaveChangesAsync(cancellationToken);
            throw SessionExpired();
        }

        var generated = refreshTokenGenerator.Generate();
        var next = session.Rotate(generated.Hash, now, currentUser.UserAgent, currentUser.IpAddress);
        db.RefreshSessions.Add(next);
        var response = await BuildResponseAsync(user, next.FamilyId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return new AuthResult(response, generated.Token, next.ExpiresAtUtc);
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var hash = refreshTokenGenerator.Hash(refreshToken);
        var session = await db.RefreshSessions.SingleOrDefaultAsync(s => s.TokenHash == hash, cancellationToken);
        if (session is null)
        {
            return;
        }

        var now = timeProvider.UtcNow();
        await RevokeFamilyAsync(session.FamilyId, now, cancellationToken);
        audit.Record(SecurityEventKind.LoggedOut, session.UserId, actorUserId: session.UserId);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuthResult> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.UtcNow();
        var user = await db.UserAccounts.SingleOrDefaultAsync(u => u.Id == currentUser.UserId, cancellationToken)
            ?? throw new AppException(AppErrorKind.Unauthenticated, ErrorCodes.Unauthenticated, "ログインが必要です。");

        if (passwordHasher.Verify(user.PasswordHash, request.CurrentPassword) == PasswordCheckResult.Failed)
        {
            throw new AppException(AppErrorKind.BadRequest, ErrorCodes.CurrentPasswordInvalid, "現在のパスワードが正しくありません。",
                errors: new Dictionary<string, string[]> { ["currentPassword"] = ["現在のパスワードが正しくありません。"] });
        }

        if (request.NewPassword == request.CurrentPassword)
        {
            throw Errors.Validation("newPassword", "現在と同じパスワードは設定できません。");
        }

        PasswordPolicy.Ensure(request.NewPassword, user.Email, user.DisplayName, "newPassword");

        user.ChangePassword(passwordHasher.Hash(request.NewPassword));
        await RevokeAllSessionsAsync(user.Id, now, cancellationToken);
        var auth = await StartSessionAsync(user, now, cancellationToken);
        audit.Record(SecurityEventKind.PasswordChanged, user.Id);
        await db.SaveChangesAsync(cancellationToken);
        return auth;
    }

    /// <summary>利用者のすべてのリフレッシュセッションを失効する（停止・再発行でも使う）。</summary>
    internal static async Task RevokeAllSessionsAsync(IAppDbContext db, Guid userId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var sessions = await db.RefreshSessions
            .Where(s => s.UserId == userId && s.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var s in sessions)
        {
            s.Revoke(nowUtc);
        }
    }

    private Task RevokeAllSessionsAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken) =>
        RevokeAllSessionsAsync(db, userId, nowUtc, cancellationToken);

    private async Task RevokeFamilyAsync(Guid familyId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var sessions = await db.RefreshSessions
            .Where(s => s.FamilyId == familyId && s.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var s in sessions)
        {
            s.Revoke(nowUtc);
        }
    }

    private async Task<AuthResult> StartSessionAsync(UserAccount user, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var generated = refreshTokenGenerator.Generate();
        var session = RefreshSession.StartFamily(user.Id, generated.Hash, nowUtc, currentUser.UserAgent, currentUser.IpAddress);
        db.RefreshSessions.Add(session);
        var response = await BuildResponseAsync(user, session.FamilyId, cancellationToken);
        return new AuthResult(response, generated.Token, session.ExpiresAtUtc);
    }

    private async Task<LoginResponse> BuildResponseAsync(UserAccount user, Guid familyId, CancellationToken cancellationToken)
    {
        var token = accessTokenIssuer.Issue(user, familyId);
        var currentUserResponse = await MyAccountService.ToCurrentUserAsync(db, user, cancellationToken);
        return new LoginResponse(token.Value, token.ExpiresInSeconds, currentUserResponse);
    }
}
