using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Common;
using TaskManager.Domain.Accounts;
using TaskManager.Domain.Common;

namespace TaskManager.Application.Users;

/// <summary>ログイン中の利用者自身の情報（REQ-USR-07）。</summary>
public sealed class MyAccountService(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<CurrentUserResponse> GetAsync(CancellationToken cancellationToken)
    {
        var user = await LoadAsync(cancellationToken);
        return await ToCurrentUserAsync(db, user, cancellationToken);
    }

    public async Task<CurrentUserResponse> UpdateProfileAsync(UpdateMeRequest request, CancellationToken cancellationToken)
    {
        var user = await LoadAsync(cancellationToken);
        try
        {
            if (request.IsSpecified(nameof(UpdateMeRequest.DisplayName)))
            {
                user.Rename(request.DisplayName ?? string.Empty);
            }
        }
        catch (DomainException ex)
        {
            throw Errors.FromDomain(ex);
        }

        if (request.IsSpecified(nameof(UpdateMeRequest.TimeZoneId)))
        {
            if (!TimeZones.IsValid(request.TimeZoneId))
            {
                throw Errors.Validation("timeZoneId", "タイムゾーンが正しくありません。一覧から選んでください。");
            }

            user.ChangeTimeZone(request.TimeZoneId!);
        }

        await db.SaveChangesAsync(cancellationToken);
        return await ToCurrentUserAsync(db, user, cancellationToken);
    }

    internal static async Task<CurrentUserResponse> ToCurrentUserAsync(
        IAppDbContext db, UserAccount user, CancellationToken cancellationToken)
    {
        var isOwner = await db.ProjectMemberships.AsNoTracking()
            .AnyAsync(m => m.UserId == user.Id && m.Role == ProjectRole.Owner, cancellationToken);
        return new CurrentUserResponse(
            user.Id, user.Email, user.DisplayName, user.SystemRole, user.TimeZoneId, user.MustChangePassword, isOwner);
    }

    private async Task<UserAccount> LoadAsync(CancellationToken cancellationToken) =>
        await db.UserAccounts.SingleOrDefaultAsync(u => u.Id == currentUser.UserId, cancellationToken)
        ?? throw new AppException(AppErrorKind.Unauthenticated, ErrorCodes.Unauthenticated, "ログインが必要です。");
}
