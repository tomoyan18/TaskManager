using TaskManager.Domain.Common;

namespace TaskManager.Domain.Accounts;

/// <summary>利用者（domain-model.md §4.1）。行は削除せず、利用停止で無効化する。</summary>
public sealed class UserAccount : IAuditable, IVersioned
{
    public const int DisplayNameMaxLength = 50;
    public const int MaxFailedLoginAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    public const string DefaultTimeZoneId = "Asia/Tokyo";

    private UserAccount()
    {
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public SystemRole SystemRole { get; private set; }

    public string PasswordHash { get; private set; } = string.Empty;

    public bool MustChangePassword { get; private set; }

    public bool IsActive { get; private set; }

    public byte FailedLoginCount { get; private set; }

    public DateTime? LockedUntilUtc { get; private set; }

    public DateTime? LastLoginAtUtc { get; private set; }

    public string TimeZoneId { get; private set; } = DefaultTimeZoneId;

    public Guid SecurityStamp { get; private set; }

    public uint RowVersion { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    /// <summary>新しい利用者を一時パスワード状態で作成する。</summary>
    public static UserAccount Create(EmailAddress email, string displayName, SystemRole role, string passwordHash)
    {
        return new UserAccount
        {
            Id = Guid.CreateVersion7(),
            Email = email.Value,
            DisplayName = NormalizeDisplayName(displayName),
            SystemRole = role,
            PasswordHash = passwordHash,
            MustChangePassword = true,
            IsActive = true,
            SecurityStamp = Guid.NewGuid(),
        };
    }

    public static string NormalizeDisplayName(string? displayName) =>
        TextRules.RequireSingleLine(displayName, DisplayNameMaxLength, "validation.failed", "displayName", "表示名");

    /// <summary>U-01: ログイン可能か（有効かつロック中でない）。</summary>
    public bool IsLockedAt(DateTime nowUtc) => LockedUntilUtc is { } until && until > nowUtc;

    /// <summary>U-02: ログイン失敗を記録する。ロックに至った場合は true。</summary>
    public bool RegisterFailedLogin(DateTime nowUtc)
    {
        FailedLoginCount++;
        if (FailedLoginCount < MaxFailedLoginAttempts)
        {
            return false;
        }

        LockedUntilUtc = nowUtc.Add(LockoutDuration);
        FailedLoginCount = 0;
        return true;
    }

    /// <summary>U-03: ログイン成功を記録する。</summary>
    public void RegisterSuccessfulLogin(DateTime nowUtc)
    {
        FailedLoginCount = 0;
        LockedUntilUtc = null;
        LastLoginAtUtc = nowUtc;
    }

    public void Rehash(string passwordHash) => PasswordHash = passwordHash;

    /// <summary>本人によるパスワード変更。U-04: セキュリティスタンプを更新する。</summary>
    public void ChangePassword(string passwordHash)
    {
        PasswordHash = passwordHash;
        MustChangePassword = false;
        SecurityStamp = Guid.NewGuid();
    }

    /// <summary>管理者による一時パスワードの再発行（変更を強制し、ロックも解除する）。</summary>
    public void ResetPassword(string temporaryPasswordHash)
    {
        PasswordHash = temporaryPasswordHash;
        MustChangePassword = true;
        FailedLoginCount = 0;
        LockedUntilUtc = null;
        SecurityStamp = Guid.NewGuid();
    }

    public void Unlock()
    {
        FailedLoginCount = 0;
        LockedUntilUtc = null;
    }

    public void Deactivate()
    {
        IsActive = false;
        SecurityStamp = Guid.NewGuid();
    }

    public void Activate() => IsActive = true;

    public void Rename(string displayName) => DisplayName = NormalizeDisplayName(displayName);

    public void ChangeSystemRole(SystemRole role) => SystemRole = role;

    public void ChangeTimeZone(string timeZoneId) => TimeZoneId = timeZoneId;
}
