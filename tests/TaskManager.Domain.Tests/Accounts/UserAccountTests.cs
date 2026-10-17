using TaskManager.Domain.Accounts;
using TaskManager.Domain.Common;

namespace TaskManager.Domain.Tests.Accounts;

public class UserAccountTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);

    private static UserAccount NewUser() =>
        UserAccount.Create(EmailAddress.Create("member@example.test"), "テスト 担当者", SystemRole.Member, "hash-1");

    [Fact]
    public void Create_一時パスワード状態の有効な利用者を作る()
    {
        var user = NewUser();
        Assert.True(user.MustChangePassword);
        Assert.True(user.IsActive);
        Assert.Equal("member@example.test", user.Email);
        Assert.Equal(UserAccount.DefaultTimeZoneId, user.TimeZoneId);
        Assert.NotEqual(Guid.Empty, user.SecurityStamp);
    }

    [Theory]
    [InlineData("")]
    [InlineData("改行\nあり")]
    public void Create_表示名の検証(string displayName)
    {
        var ex = Assert.Throws<DomainException>(() =>
            UserAccount.Create(EmailAddress.Create("a@example.test"), displayName, SystemRole.Member, "h"));
        Assert.Equal("displayName", ex.Field);
    }

    // U-02: 5 回失敗で 15 分ロック
    [Fact]
    public void RegisterFailedLogin_4回まではロックせず5回目でロックする()
    {
        var user = NewUser();
        for (var i = 1; i <= 4; i++)
        {
            Assert.False(user.RegisterFailedLogin(Now));
            Assert.False(user.IsLockedAt(Now));
        }

        Assert.True(user.RegisterFailedLogin(Now));
        Assert.True(user.IsLockedAt(Now));
        Assert.Equal(Now.AddMinutes(15), user.LockedUntilUtc);
        Assert.Equal(0, user.FailedLoginCount);
    }

    [Fact]
    public void IsLockedAt_ロック期限ちょうどで解除される()
    {
        var user = NewUser();
        for (var i = 0; i < 5; i++)
        {
            user.RegisterFailedLogin(Now);
        }

        Assert.True(user.IsLockedAt(Now.AddMinutes(15).AddTicks(-1)));
        Assert.False(user.IsLockedAt(Now.AddMinutes(15)));
    }

    [Fact]
    public void RegisterSuccessfulLogin_失敗回数とロックを消す()
    {
        var user = NewUser();
        user.RegisterFailedLogin(Now);
        user.RegisterSuccessfulLogin(Now);
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.LockedUntilUtc);
        Assert.Equal(Now, user.LastLoginAtUtc);
    }

    // U-04: パスワード変更でスタンプ更新
    [Fact]
    public void ChangePassword_一時パスワード状態を解除しスタンプを変える()
    {
        var user = NewUser();
        var stamp = user.SecurityStamp;
        user.ChangePassword("hash-2");
        Assert.False(user.MustChangePassword);
        Assert.Equal("hash-2", user.PasswordHash);
        Assert.NotEqual(stamp, user.SecurityStamp);
    }

    [Fact]
    public void ResetPassword_変更を強制しロックを解除しスタンプを変える()
    {
        var user = NewUser();
        user.ChangePassword("hash-2");
        for (var i = 0; i < 5; i++)
        {
            user.RegisterFailedLogin(Now);
        }

        var stamp = user.SecurityStamp;
        user.ResetPassword("temp-hash");
        Assert.True(user.MustChangePassword);
        Assert.False(user.IsLockedAt(Now));
        Assert.NotEqual(stamp, user.SecurityStamp);
    }

    [Fact]
    public void Deactivate_スタンプを変えて既存のトークンを無効にする()
    {
        var user = NewUser();
        var stamp = user.SecurityStamp;
        user.Deactivate();
        Assert.False(user.IsActive);
        Assert.NotEqual(stamp, user.SecurityStamp);
        user.Activate();
        Assert.True(user.IsActive);
    }
}

public class RefreshSessionTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void StartFamily_有効期限は14日で絶対期限は30日()
    {
        var session = RefreshSession.StartFamily(Guid.NewGuid(), new byte[32], Now, "agent", "127.0.0.1");
        Assert.Equal(Now.AddDays(14), session.ExpiresAtUtc);
        Assert.Equal(Now.AddDays(30), session.AbsoluteExpiresAtUtc);
        Assert.True(session.IsUsableAt(Now));
        Assert.False(session.IsUsableAt(Now.AddDays(14)));
    }

    [Fact]
    public void Rotate_同じ系列で次を作り元は使えなくなる()
    {
        var first = RefreshSession.StartFamily(Guid.NewGuid(), new byte[32], Now, null, null);
        var next = first.Rotate(new byte[32], Now.AddDays(1), null, null);
        Assert.Equal(first.FamilyId, next.FamilyId);
        Assert.Equal(first.UserId, next.UserId);
        Assert.True(first.IsRotated);
        Assert.False(first.IsUsableAt(Now.AddDays(1)));
        Assert.True(next.IsUsableAt(Now.AddDays(1)));
    }

    [Fact]
    public void Rotate_絶対期限を超えて延長しない()
    {
        var first = RefreshSession.StartFamily(Guid.NewGuid(), new byte[32], Now, null, null);
        var late = first.Rotate(new byte[32], Now.AddDays(25), null, null);
        Assert.Equal(Now.AddDays(30), late.ExpiresAtUtc);
        Assert.False(late.IsUsableAt(Now.AddDays(30)));
    }

    [Fact]
    public void Revoke_使えなくなり最初の失効日時を保つ()
    {
        var session = RefreshSession.StartFamily(Guid.NewGuid(), new byte[32], Now, null, null);
        session.Revoke(Now.AddHours(1));
        session.Revoke(Now.AddHours(2));
        Assert.Equal(Now.AddHours(1), session.RevokedAtUtc);
        Assert.False(session.IsUsableAt(Now.AddHours(1)));
    }

    [Fact]
    public void StartFamily_長い利用者エージェントは切り詰める()
    {
        var session = RefreshSession.StartFamily(Guid.NewGuid(), new byte[32], Now, new string('x', 300), "");
        Assert.Equal(RefreshSession.UserAgentMaxLength, session.UserAgent!.Length);
        Assert.Null(session.CreatedFromIp);
    }
}
