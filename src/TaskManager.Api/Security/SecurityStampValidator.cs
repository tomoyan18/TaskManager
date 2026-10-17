using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TaskManager.Infrastructure.Persistence;

namespace TaskManager.Api.Security;

/// <summary>
/// アクセストークンの即時失効（authentication-authorization.md §2.1）。トークンの stamp と DB の security_stamp を照合し、
/// 不一致・利用停止なら無効にする。DB への問い合わせは 30 秒キャッシュする。
/// </summary>
public sealed class SecurityStampValidator(AppDbContext db, IMemoryCache cache)
{
    public static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public async Task<bool> IsValidAsync(Guid userId, string? stamp)
    {
        if (!Guid.TryParse(stamp, out var tokenStamp))
        {
            return false;
        }

        var current = await cache.GetOrCreateAsync($"stamp:{userId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            // 結果はキャッシュで他のリクエストと共有するため、個々のリクエストの中断では取り消さない（主キー検索 1 件で短い）。
            // MySql.Data は SSL 接続確立中の取り消しでコールバックから例外を投げ、Kestrel が未処理例外として記録する（Phase 5 で確認）
            return await db.UserAccounts.AsNoTracking().Where(u => u.Id == userId)
                .Select(u => new StampState(u.SecurityStamp, u.IsActive)).SingleOrDefaultAsync(CancellationToken.None);
        });
        return current is not null && current.IsActive && current.Stamp == tokenStamp;
    }

    /// <summary>パスワード変更・停止などで security_stamp を変えた直後に呼ぶ。</summary>
    public void Invalidate(Guid userId) => cache.Remove($"stamp:{userId}");

    private sealed record StampState(Guid Stamp, bool IsActive);
}
