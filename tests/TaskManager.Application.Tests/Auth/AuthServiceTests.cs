using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Auth;
using TaskManager.Application.Common;
using TaskManager.Application.Users;
using TaskManager.Domain.Common;
using TaskManager.Testing;

namespace TaskManager.Application.Tests.Auth;

public class AuthServiceTests(DatabaseFixture fixture) : ApplicationTestBase(fixture)
{
    private Task<AuthResult> LoginAsync(string email, string password) =>
        Host.RunAsync<AuthService, AuthResult>(null, s => s.LoginAsync(new LoginRequest { Email = email, Password = password }, Ct));

    private Task<AuthResult> RefreshAsync(string? token) =>
        Host.RunAsync<AuthService, AuthResult>(null, s => s.RefreshAsync(token, Ct));

    private Task<List<SecurityEventKind>> AuditKindsAsync(Guid userId) =>
        Data.QueryAsync(db => db.SecurityAuditEvents.Where(e => e.SubjectUserId == userId)
            .OrderBy(e => e.OccurredAtUtc).Select(e => e.Kind).ToListAsync(Ct));

    [Fact]
    public async Task Login_正しいパスワードでトークンとリフレッシュトークンを返す()
    {
        var user = await Data.CreateUserAsync();

        var result = await LoginAsync("  " + user.Email.ToUpperInvariant() + " ", user.Password);

        Assert.StartsWith("test-token:" + user.Id, result.Response.AccessToken, StringComparison.Ordinal);
        Assert.Equal(900, result.Response.ExpiresIn);
        Assert.Equal(user.Id, result.Response.User.Id);
        Assert.False(string.IsNullOrEmpty(result.RefreshToken));
        Assert.Equal(Host.Time.GetUtcNow().UtcDateTime.AddDays(14), result.RefreshExpiresAtUtc);
        var session = await Data.QueryAsync(db => db.RefreshSessions.SingleAsync(s => s.UserId == user.Id, Ct));
        Assert.NotEqual(result.RefreshToken, Convert.ToBase64String(session.TokenHash));
        Assert.Contains(SecurityEventKind.LoginSucceeded, await AuditKindsAsync(user.Id));
    }

    [Fact]
    public async Task Login_誤ったパスワードと存在しない利用者は同じエラー()
    {
        var user = await Data.CreateUserAsync();

        var wrong = await AppAssert.ThrowsAsync(AppErrorKind.Unauthenticated, ErrorCodes.InvalidCredentials,
            () => LoginAsync(user.Email, "wrong-password-123"));
        var unknown = await AppAssert.ThrowsAsync(AppErrorKind.Unauthenticated, ErrorCodes.InvalidCredentials,
            () => LoginAsync(Unique.Email("unknown"), "wrong-password-123"));

        Assert.Equal(wrong.Title, unknown.Title);
        Assert.Equal(SecurityEventKind.LoginFailed, Assert.Single(await AuditKindsAsync(user.Id)));
    }

    [Fact]
    public async Task Login_5回失敗するとロックされ正しいパスワードでもログインできない()
    {
        var user = await Data.CreateUserAsync();
        for (var i = 0; i < 5; i++)
        {
            await AppAssert.ThrowsAsync(AppErrorKind.Unauthenticated, ErrorCodes.InvalidCredentials,
                () => LoginAsync(user.Email, "wrong-password-123"));
        }

        var locked = await AppAssert.ThrowsAsync(AppErrorKind.Unauthenticated, ErrorCodes.Locked, () => LoginAsync(user.Email, user.Password));
        Assert.NotNull(locked.Detail);
        Assert.Contains(SecurityEventKind.AccountLocked, await AuditKindsAsync(user.Id));

        Host.Time.Advance(TimeSpan.FromMinutes(15));
        var result = await LoginAsync(user.Email, user.Password);
        Assert.Equal(user.Id, result.Response.User.Id);
    }

    [Fact]
    public async Task Login_利用停止中はログインできない()
    {
        var user = await Data.CreateUserAsync(active: false);
        await AppAssert.ThrowsAsync(AppErrorKind.Unauthenticated, ErrorCodes.InvalidCredentials, () => LoginAsync(user.Email, user.Password));
    }

    [Fact]
    public async Task Login_一時パスワード状態は変更が必要なことを返す()
    {
        var user = await Data.CreateUserAsync(mustChangePassword: true);
        var result = await LoginAsync(user.Email, user.Password);
        Assert.True(result.Response.User.MustChangePassword);
    }

    [Fact]
    public async Task Refresh_ローテーションし古いトークンの再利用で系列ごと失効する()
    {
        var user = await Data.CreateUserAsync();
        var login = await LoginAsync(user.Email, user.Password);

        Host.Time.Advance(TimeSpan.FromMinutes(10));
        var refreshed = await RefreshAsync(login.RefreshToken);
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);

        // 使用済みトークンの再提示 → 盗用の疑い
        await AppAssert.ThrowsAsync(AppErrorKind.Unauthenticated, ErrorCodes.SessionExpired, () => RefreshAsync(login.RefreshToken));
        await AppAssert.ThrowsAsync(AppErrorKind.Unauthenticated, ErrorCodes.SessionExpired, () => RefreshAsync(refreshed.RefreshToken));
        Assert.Contains(SecurityEventKind.RefreshTokenReuseDetected, await AuditKindsAsync(user.Id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-real-token")]
    public async Task Refresh_トークンがないか不明なら期限切れ(string? token)
    {
        await AppAssert.ThrowsAsync(AppErrorKind.Unauthenticated, ErrorCodes.SessionExpired, () => RefreshAsync(token));
    }

    [Fact]
    public async Task Refresh_14日を過ぎると期限切れ()
    {
        var user = await Data.CreateUserAsync();
        var login = await LoginAsync(user.Email, user.Password);
        Host.Time.Advance(TimeSpan.FromDays(14));
        await AppAssert.ThrowsAsync(AppErrorKind.Unauthenticated, ErrorCodes.SessionExpired, () => RefreshAsync(login.RefreshToken));
    }

    [Fact]
    public async Task Refresh_利用停止された利用者は期限切れ()
    {
        var admin = await Data.CreateUserAsync(administrator: true);
        var user = await Data.CreateUserAsync();
        var login = await LoginAsync(user.Email, user.Password);
        await Host.RunAsync<UserAdministrationService>(admin, s => s.DeactivateAsync(user.Id, Ct));

        await AppAssert.ThrowsAsync(AppErrorKind.Unauthenticated, ErrorCodes.SessionExpired, () => RefreshAsync(login.RefreshToken));
    }

    [Fact]
    public async Task Logout_同じ系列のトークンを失効する()
    {
        var user = await Data.CreateUserAsync();
        var login = await LoginAsync(user.Email, user.Password);
        await Host.RunAsync<AuthService>(null, s => s.LogoutAsync(login.RefreshToken, Ct));

        await AppAssert.ThrowsAsync(AppErrorKind.Unauthenticated, ErrorCodes.SessionExpired, () => RefreshAsync(login.RefreshToken));
        Assert.Contains(SecurityEventKind.LoggedOut, await AuditKindsAsync(user.Id));
    }

    [Fact]
    public async Task Logout_トークンなしや不明なトークンでもエラーにしない()
    {
        await Host.RunAsync<AuthService>(null, s => s.LogoutAsync(null, Ct));
        await Host.RunAsync<AuthService>(null, s => s.LogoutAsync("unknown", Ct));
    }

    [Fact]
    public async Task ChangePassword_成功すると一時パスワード状態を解除し他のセッションを失効する()
    {
        var user = await Data.CreateUserAsync(mustChangePassword: true);
        var otherDevice = await LoginAsync(user.Email, user.Password);
        const string newPassword = "Kanagawa-Minato-2026";

        var result = await Host.RunAsync<AuthService, AuthResult>(user, s => s.ChangePasswordAsync(
            new ChangePasswordRequest { CurrentPassword = user.Password, NewPassword = newPassword }, Ct));

        Assert.False(result.Response.User.MustChangePassword);
        await AppAssert.ThrowsAsync(AppErrorKind.Unauthenticated, ErrorCodes.SessionExpired, () => RefreshAsync(otherDevice.RefreshToken));
        await RefreshAsync(result.RefreshToken);
        await AppAssert.ThrowsAsync(AppErrorKind.Unauthenticated, ErrorCodes.InvalidCredentials, () => LoginAsync(user.Email, user.Password));
        await LoginAsync(user.Email, newPassword);
        Assert.Contains(SecurityEventKind.PasswordChanged, await AuditKindsAsync(user.Id));
    }

    [Fact]
    public async Task ChangePassword_現在のパスワードが違うと項目エラー()
    {
        var user = await Data.CreateUserAsync();
        var ex = await AppAssert.ThrowsAsync(AppErrorKind.BadRequest, ErrorCodes.CurrentPasswordInvalid,
            () => Host.RunAsync<AuthService, AuthResult>(user, s => s.ChangePasswordAsync(
                new ChangePasswordRequest { CurrentPassword = "wrong-password-123", NewPassword = "Kanagawa-Minato-2026" }, Ct)));
        Assert.True(ex.Errors!.ContainsKey("currentPassword"));
    }

    [Theory]
    [InlineData("short-pw")]
    [InlineData("password1234")]
    [InlineData("aaaa-bbbb-cccc")]
    public async Task ChangePassword_ポリシー違反は項目エラー(string newPassword)
    {
        var user = await Data.CreateUserAsync();
        await AppAssert.ValidationAsync("newPassword", () => Host.RunAsync<AuthService, AuthResult>(user, s => s.ChangePasswordAsync(
            new ChangePasswordRequest { CurrentPassword = user.Password, NewPassword = newPassword }, Ct)));
    }

    [Fact]
    public async Task ChangePassword_現在と同じパスワードは使えない()
    {
        var user = await Data.CreateUserAsync(password: "Same-Password-2026");
        await AppAssert.ValidationAsync("newPassword", () => Host.RunAsync<AuthService, AuthResult>(user, s => s.ChangePasswordAsync(
            new ChangePasswordRequest { CurrentPassword = user.Password, NewPassword = user.Password }, Ct)));
    }
}
