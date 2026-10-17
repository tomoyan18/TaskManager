using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Auth;
using TaskManager.Application.Common;
using TaskManager.Application.Users;
using TaskManager.Domain.Common;
using TaskManager.Testing;

namespace TaskManager.Application.Tests.Users;

public class UserAdministrationServiceTests(DatabaseFixture fixture) : ApplicationTestBase(fixture)
{
    [Fact]
    public async Task Create_一時パスワードを発行し初回ログインで変更を求める()
    {
        var admin = await Data.CreateUserAsync(administrator: true);
        var email = Unique.Email("new");

        var created = await Host.RunAsync<UserAdministrationService, CreatedUserResponse>(admin, s => s.CreateAsync(
            new CreateUserRequest { Email = email.ToUpperInvariant(), DisplayName = "新しい 担当者", SystemRole = SystemRole.Member }, Ct));

        Assert.Equal(email, created.User.Email);
        Assert.True(created.User.MustChangePassword);
        Assert.True(created.TemporaryPassword.Length >= 12);
        var login = await Host.RunAsync<AuthService, AuthResult>(null, s => s.LoginAsync(
            new LoginRequest { Email = email, Password = created.TemporaryPassword }, Ct));
        Assert.True(login.Response.User.MustChangePassword);
        var hash = await Data.QueryAsync(db => db.UserAccounts.Where(u => u.Email == email).Select(u => u.PasswordHash).SingleAsync(Ct));
        Assert.DoesNotContain(created.TemporaryPassword, hash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_同じメールアドレスは409()
    {
        var admin = await Data.CreateUserAsync(administrator: true);
        var existing = await Data.CreateUserAsync();
        await AppAssert.ThrowsAsync(AppErrorKind.Conflict, ErrorCodes.UserEmailTaken, () => Host.RunAsync<UserAdministrationService, CreatedUserResponse>(
            admin, s => s.CreateAsync(new CreateUserRequest { Email = existing.Email, DisplayName = "重複", SystemRole = SystemRole.Member }, Ct)));
    }

    [Fact]
    public async Task Deactivate_自分自身は停止できない()
    {
        var admin = await Data.CreateUserAsync(administrator: true);
        await AppAssert.ThrowsAsync(AppErrorKind.Conflict, ErrorCodes.UserCannotDeactivateSelf,
            () => Host.RunAsync<UserAdministrationService>(admin, s => s.DeactivateAsync(admin.Id, Ct)));
    }

    [Fact]
    public async Task ResetPassword_セッションを失効し一時パスワード状態にする()
    {
        var admin = await Data.CreateUserAsync(administrator: true);
        var user = await Data.CreateUserAsync();
        var login = await Host.RunAsync<AuthService, AuthResult>(null, s => s.LoginAsync(new LoginRequest { Email = user.Email, Password = user.Password }, Ct));

        var reset = await Host.RunAsync<UserAdministrationService, TemporaryPasswordResponse>(admin, s => s.ResetPasswordAsync(user.Id, Ct));

        await AppAssert.ThrowsAsync(AppErrorKind.Unauthenticated, ErrorCodes.SessionExpired,
            () => Host.RunAsync<AuthService, AuthResult>(null, s => s.RefreshAsync(login.RefreshToken, Ct)));
        var relogin = await Host.RunAsync<AuthService, AuthResult>(null, s => s.LoginAsync(new LoginRequest { Email = user.Email, Password = reset.TemporaryPassword }, Ct));
        Assert.True(relogin.Response.User.MustChangePassword);
    }

    [Fact]
    public async Task Unlock_ロックを解除する()
    {
        var admin = await Data.CreateUserAsync(administrator: true);
        var user = await Data.CreateUserAsync();
        for (var i = 0; i < 5; i++)
        {
            await Assert.ThrowsAsync<AppException>(() => Host.RunAsync<AuthService, AuthResult>(null, s => s.LoginAsync(
                new LoginRequest { Email = user.Email, Password = "wrong-password-123" }, Ct)));
        }

        Assert.True((await Host.RunAsync<UserAdministrationService, UserAdminDetailResponse>(admin, s => s.GetAsync(user.Id, Ct))).IsLocked);
        await Host.RunAsync<UserAdministrationService>(admin, s => s.UnlockAsync(user.Id, Ct));
        await Host.RunAsync<AuthService, AuthResult>(null, s => s.LoginAsync(new LoginRequest { Email = user.Email, Password = user.Password }, Ct));
    }

    [Fact]
    public async Task Update_古い版は409()
    {
        var admin = await Data.CreateUserAsync(administrator: true);
        var user = await Data.CreateUserAsync();
        var detail = await Host.RunAsync<UserAdministrationService, UserAdminDetailResponse>(admin, s => s.GetAsync(user.Id, Ct));
        await Host.RunAsync<UserAdministrationService, UserAdminDetailResponse>(admin, s => s.UpdateAsync(
            user.Id, new UpdateUserRequest { DisplayName = "改名後", Version = detail.Version }, Ct));

        await AppAssert.ThrowsAsync(AppErrorKind.Conflict, ErrorCodes.ConcurrencyConflict, () => Host.RunAsync<UserAdministrationService, UserAdminDetailResponse>(
            admin, s => s.UpdateAsync(user.Id, new UpdateUserRequest { DisplayName = "再改名", Version = detail.Version }, Ct)));
    }

    [Fact]
    public async Task List_許可されていない並べ替えは400()
    {
        var admin = await Data.CreateUserAsync(administrator: true);
        await AppAssert.ThrowsAsync(AppErrorKind.BadRequest, ErrorCodes.InvalidSort, () => Host.RunAsync<UserAdministrationService, PagedResponse<UserAdminSummaryResponse>>(
            admin, s => s.ListAsync(null, null, "passwordHash", PageRequest.Of(1, 20, 20), Ct)));
    }
}

/// <summary>DB 全体の管理者の有無に依存するため、専用の DB で実行する。</summary>
public class LastAdministratorTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private ApplicationTestHost _host = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _database = await fixture.CreateIsolatedDatabaseAsync();
        _host = new ApplicationTestHost(_database);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.DisposeAsync();
        await _database.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task 最後の有効な管理者は停止も降格もできない()
    {
        var admin1 = await _host.Data.CreateUserAsync(administrator: true);
        var admin2 = await _host.Data.CreateUserAsync(administrator: true);

        await _host.RunAsync<UserAdministrationService>(admin1, s => s.DeactivateAsync(admin2.Id, Ct));

        await AppAssert.ThrowsAsync(AppErrorKind.Conflict, ErrorCodes.UserLastAdministrator, () => _host.RunAsync<UserAdministrationService, UserAdminDetailResponse>(
            admin1, s => s.UpdateAsync(admin1.Id, new UpdateUserRequest { SystemRole = SystemRole.Member, Version = 0 }, Ct)));
    }

    [Fact]
    public async Task 初期管理者は管理者がいない場合だけ作成する()
    {
        var created = Unique.Email("bootstrap");
        await _host.RunAsync<BootstrapAdministratorService>(null, s => s.EnsureAsync(created, "Bootstrap-Admin-2026", Ct));
        await _host.RunAsync<BootstrapAdministratorService>(null, s => s.EnsureAsync(Unique.Email("second"), "Bootstrap-Admin-2026", Ct));

        var admins = await _host.Data.QueryAsync(db => db.UserAccounts.Where(u => u.SystemRole == SystemRole.Administrator).ToListAsync(Ct));
        var admin = Assert.Single(admins);
        Assert.Equal(created, admin.Email);
        Assert.True(admin.MustChangePassword);
    }

    [Theory]
    [InlineData("admin@example.test", "short")]
    [InlineData("not-an-email", "Bootstrap-Admin-2026")]
    [InlineData(null, null)]
    public async Task 初期管理者の設定が不正なら作成しない(string? email, string? password)
    {
        await _host.RunAsync<BootstrapAdministratorService>(null, s => s.EnsureAsync(email, password, Ct));
        Assert.False(await _host.Data.QueryAsync(db => db.UserAccounts.AnyAsync(Ct)));
    }
}

public class MyAccountServiceTests(DatabaseFixture fixture) : ApplicationTestBase(fixture)
{
    [Fact]
    public async Task UpdateProfile_不正なタイムゾーンは項目エラー()
    {
        var user = await Data.CreateUserAsync();
        await AppAssert.ValidationAsync("timeZoneId", () => Host.RunAsync<MyAccountService, CurrentUserResponse>(
            user, s => s.UpdateProfileAsync(new UpdateMeRequest { TimeZoneId = "Mars/Olympus" }, Ct)));
    }

    [Fact]
    public async Task UpdateProfile_表示名を変える()
    {
        var user = await Data.CreateUserAsync();
        var updated = await Host.RunAsync<MyAccountService, CurrentUserResponse>(
            user, s => s.UpdateProfileAsync(new UpdateMeRequest { DisplayName = " 新しい表示名 " }, Ct));
        Assert.Equal("新しい表示名", updated.DisplayName);
        Assert.Equal(UserAccountDefaults.TimeZone, updated.TimeZoneId);
    }

    private static class UserAccountDefaults
    {
        public const string TimeZone = "Asia/Tokyo";
    }
}
