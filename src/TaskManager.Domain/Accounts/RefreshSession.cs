namespace TaskManager.Domain.Accounts;

/// <summary>リフレッシュトークン 1 件（domain-model.md §4.2）。トークンはハッシュのみ保存する。</summary>
public sealed class RefreshSession
{
    public static readonly TimeSpan SlidingLifetime = TimeSpan.FromDays(14);
    public static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromDays(30);
    public const int UserAgentMaxLength = 255;
    public const int IpAddressMaxLength = 45;

    private RefreshSession()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid FamilyId { get; private set; }

#pragma warning disable CA1819 // EF Core の BINARY(32) 列に対応させるため配列を公開する
    public byte[] TokenHash { get; private set; } = [];
#pragma warning restore CA1819

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime AbsoluteExpiresAtUtc { get; private set; }

    public DateTime? RotatedAtUtc { get; private set; }

    public DateTime? RevokedAtUtc { get; private set; }

    public string? UserAgent { get; private set; }

    public string? CreatedFromIp { get; private set; }

    /// <summary>ログイン時に新しい系列のセッションを作る。</summary>
    public static RefreshSession StartFamily(Guid userId, byte[] tokenHash, DateTime nowUtc, string? userAgent, string? ip) =>
        Create(userId, Guid.CreateVersion7(), tokenHash, nowUtc, nowUtc.Add(AbsoluteLifetime), userAgent, ip);

    /// <summary>ローテーション時に同じ系列で次のセッションを作る。</summary>
    public RefreshSession Rotate(byte[] newTokenHash, DateTime nowUtc, string? userAgent, string? ip)
    {
        RotatedAtUtc = nowUtc;
        return Create(UserId, FamilyId, newTokenHash, nowUtc, AbsoluteExpiresAtUtc, userAgent, ip);
    }

    /// <summary>R-01: 使用可能か。</summary>
    public bool IsUsableAt(DateTime nowUtc) =>
        RotatedAtUtc is null && RevokedAtUtc is null && ExpiresAtUtc > nowUtc && AbsoluteExpiresAtUtc > nowUtc;

    public bool IsRotated => RotatedAtUtc is not null;

    public void Revoke(DateTime nowUtc) => RevokedAtUtc ??= nowUtc;

    private static RefreshSession Create(
        Guid userId, Guid familyId, byte[] tokenHash, DateTime nowUtc, DateTime absoluteExpiresAtUtc, string? userAgent, string? ip)
    {
        var expires = nowUtc.Add(SlidingLifetime);
        return new RefreshSession
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            FamilyId = familyId,
            TokenHash = tokenHash,
            CreatedAtUtc = nowUtc,
            ExpiresAtUtc = expires < absoluteExpiresAtUtc ? expires : absoluteExpiresAtUtc,
            AbsoluteExpiresAtUtc = absoluteExpiresAtUtc,
            UserAgent = Truncate(userAgent, UserAgentMaxLength),
            CreatedFromIp = Truncate(ip, IpAddressMaxLength),
        };
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? null : value.Length <= max ? value : value[..max];
}
