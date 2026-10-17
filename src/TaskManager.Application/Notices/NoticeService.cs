using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Common;
using TaskManager.Domain.Common;

namespace TaskManager.Application.Notices;

public sealed record NoticeResponse(
    Guid Id, NoticeKind Kind, string Message, string? LinkPath, UserRef? Actor, bool IsRead, DateTime CreatedAtUtc);

public sealed record UnreadNoticeCountResponse(int Count);

/// <summary>自分宛てのお知らせ（REQ-NTC-01）。常に宛先 = 現在の利用者で絞る。</summary>
public sealed class NoticeService(IAppDbContext db, ICurrentUser currentUser, TimeProvider timeProvider)
{
    public async Task<PagedResponse<NoticeResponse>> ListAsync(bool unreadOnly, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var paging = PageRequest.Of(page, pageSize, 30);
        var query = db.Notices.AsNoTracking().Where(n => n.RecipientUserId == currentUser.UserId);
        if (unreadOnly)
        {
            query = query.Where(n => n.ReadAtUtc == null);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(n => n.CreatedAtUtc).ThenByDescending(n => n.Id)
            .Skip(paging.Skip).Take(paging.PageSize).ToListAsync(cancellationToken);
        var actorIds = rows.Where(r => r.ActorUserId != null).Select(r => r.ActorUserId!.Value).Distinct().ToList();
        var actors = await db.UserAccounts.AsNoTracking().Where(u => actorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new UserRef(u.Id, u.DisplayName, u.IsActive), cancellationToken);
        var items = rows.Select(n => new NoticeResponse(
            n.Id, n.Kind, n.Message, n.TaskId is null && n.Kind != NoticeKind.AddedToProject ? null : n.LinkPath,
            n.ActorUserId is { } a ? actors.GetValueOrDefault(a) : null, n.IsRead, n.CreatedAtUtc)).ToList();
        return PagedResponse<NoticeResponse>.Create(items, paging, total);
    }

    public async Task<UnreadNoticeCountResponse> CountUnreadAsync(CancellationToken cancellationToken) =>
        new(await db.Notices.AsNoTracking().CountAsync(n => n.RecipientUserId == currentUser.UserId && n.ReadAtUtc == null, cancellationToken));

    public async Task MarkReadAsync(Guid noticeId, CancellationToken cancellationToken)
    {
        var notice = await db.Notices.SingleOrDefaultAsync(n => n.Id == noticeId && n.RecipientUserId == currentUser.UserId, cancellationToken)
            ?? throw Errors.NotFound();
        notice.MarkRead(timeProvider.UtcNow());
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllReadAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.UtcNow();
        await db.Notices.Where(n => n.RecipientUserId == currentUser.UserId && n.ReadAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAtUtc, now), cancellationToken);
    }
}
