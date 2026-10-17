using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Auth;
using TaskManager.Application.Common;
using TaskManager.Application.Security;
using TaskManager.Domain.Accounts;
using TaskManager.Domain.Common;

namespace TaskManager.Application.Users;

/// <summary>利用者管理（システム管理者のみ。REQ-USR-01〜06）。権限は Api のポリシーで確認する。</summary>
public sealed class UserAdministrationService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IPasswordHasher passwordHasher,
    ITemporaryPasswordGenerator temporaryPasswordGenerator,
    SecurityAuditService audit,
    TimeProvider timeProvider)
{
    public static readonly IReadOnlyDictionary<string, string> SortKeys = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["displayName"] = "displayName",
        ["email"] = "email",
        ["-lastLoginAt"] = "-lastLoginAt",
        ["createdAt"] = "createdAt",
    };

    public async Task<PagedResponse<UserAdminSummaryResponse>> ListAsync(
        string? q, UserStatusFilter? status, string? sort, PageRequest page, CancellationToken cancellationToken)
    {
        var now = timeProvider.UtcNow();
        var query = db.UserAccounts.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(u => u.DisplayName.Contains(term) || u.Email.Contains(term));
        }

        query = status switch
        {
            UserStatusFilter.Active => query.Where(u => u.IsActive),
            UserStatusFilter.Inactive => query.Where(u => !u.IsActive),
            UserStatusFilter.Locked => query.Where(u => u.LockedUntilUtc != null && u.LockedUntilUtc > now),
            _ => query,
        };

        query = (sort ?? "displayName") switch
        {
            "displayName" => query.OrderBy(u => u.DisplayName).ThenBy(u => u.Email),
            "email" => query.OrderBy(u => u.Email),
            "-lastLoginAt" => query.OrderByDescending(u => u.LastLoginAtUtc).ThenBy(u => u.DisplayName),
            "createdAt" => query.OrderBy(u => u.CreatedAtUtc),
            _ => throw InvalidSort(),
        };

        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken);
        return PagedResponse<UserAdminSummaryResponse>.Create(items.Select(u => ToSummary(u, now)).ToList(), page, total);
    }

    public async Task<UserAdminDetailResponse> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.UserAccounts.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw Errors.NotFound();
        return await ToDetailAsync(user, cancellationToken);
    }

    public async Task<CreatedUserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var email = WrapDomain(() => EmailAddress.Create(request.Email));
        if (await db.UserAccounts.AnyAsync(u => u.Email == email.Value, cancellationToken))
        {
            throw Errors.Conflict(ErrorCodes.UserEmailTaken, "このメールアドレスは既に登録されています。");
        }

        var temporaryPassword = temporaryPasswordGenerator.Generate();
        var user = WrapDomain(() => UserAccount.Create(
            email, request.DisplayName, request.SystemRole ?? SystemRole.Member, passwordHasher.Hash(temporaryPassword)));
        db.UserAccounts.Add(user);
        audit.Record(SecurityEventKind.UserCreated, user.Id,
            detail: new Dictionary<string, string?> { ["systemRole"] = user.SystemRole.ToString() });
        await db.SaveChangesAsync(cancellationToken);
        return new CreatedUserResponse(ToSummary(user, timeProvider.UtcNow()), temporaryPassword);
    }

    public async Task<UserAdminDetailResponse> UpdateAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await LoadAsync(userId, cancellationToken);
        db.SetExpectedVersion(user, request.Version);
        if (user.RowVersion != request.Version)
        {
            throw Errors.ConcurrencyConflict();
        }

        if (request.IsSpecified(nameof(UpdateUserRequest.DisplayName)))
        {
            WrapDomain(() => user.Rename(request.DisplayName ?? string.Empty));
        }

        if (request.IsSpecified(nameof(UpdateUserRequest.SystemRole)) && request.SystemRole is { } role && role != user.SystemRole)
        {
            if (role != SystemRole.Administrator)
            {
                await EnsureAnotherActiveAdministratorAsync(user.Id, cancellationToken);
            }

            user.ChangeSystemRole(role);
            audit.Record(SecurityEventKind.UserUpdated, user.Id,
                detail: new Dictionary<string, string?> { ["systemRole"] = role.ToString() });
        }

        await db.SaveChangesAsync(cancellationToken);
        return await ToDetailAsync(user, cancellationToken);
    }

    public async Task DeactivateAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (userId == currentUser.UserId)
        {
            throw Errors.Conflict(ErrorCodes.UserCannotDeactivateSelf, "自分自身を利用停止にはできません。");
        }

        var user = await LoadAsync(userId, cancellationToken);
        if (!user.IsActive)
        {
            return;
        }

        if (user.SystemRole == SystemRole.Administrator)
        {
            await EnsureAnotherActiveAdministratorAsync(user.Id, cancellationToken);
        }

        var now = timeProvider.UtcNow();
        user.Deactivate();
        await AuthService.RevokeAllSessionsAsync(db, user.Id, now, cancellationToken);
        audit.Record(SecurityEventKind.UserDeactivated, user.Id);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ActivateAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await LoadAsync(userId, cancellationToken);
        if (user.IsActive)
        {
            return;
        }

        user.Activate();
        audit.Record(SecurityEventKind.UserActivated, user.Id);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UnlockAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await LoadAsync(userId, cancellationToken);
        user.Unlock();
        audit.Record(SecurityEventKind.UserUnlocked, user.Id);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<TemporaryPasswordResponse> ResetPasswordAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await LoadAsync(userId, cancellationToken);
        var temporaryPassword = temporaryPasswordGenerator.Generate();
        user.ResetPassword(passwordHasher.Hash(temporaryPassword));
        await AuthService.RevokeAllSessionsAsync(db, user.Id, timeProvider.UtcNow(), cancellationToken);
        audit.Record(SecurityEventKind.PasswordReset, user.Id);
        await db.SaveChangesAsync(cancellationToken);
        return new TemporaryPasswordResponse(temporaryPassword);
    }

    internal static AppException InvalidSort() =>
        new(AppErrorKind.BadRequest, ErrorCodes.InvalidSort, "指定された並べ替え項目は使用できません。");

    private static T WrapDomain<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch (DomainException ex)
        {
            throw Errors.FromDomain(ex);
        }
    }

    private static void WrapDomain(Action action) => WrapDomain(() =>
    {
        action();
        return true;
    });

    private async Task EnsureAnotherActiveAdministratorAsync(Guid exceptUserId, CancellationToken cancellationToken)
    {
        var hasOther = await db.UserAccounts.AnyAsync(
            u => u.Id != exceptUserId && u.IsActive && u.SystemRole == SystemRole.Administrator, cancellationToken);
        if (!hasOther)
        {
            throw Errors.Conflict(ErrorCodes.UserLastAdministrator, "システム管理者が 1 人もいなくなるため、この操作はできません。");
        }
    }

    private async Task<UserAccount> LoadAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.UserAccounts.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken) ?? throw Errors.NotFound();

    private static UserAdminSummaryResponse ToSummary(UserAccount u, DateTime nowUtc) => new(
        u.Id, u.Email, u.DisplayName, u.SystemRole, u.IsActive, u.IsLockedAt(nowUtc), u.MustChangePassword,
        u.LastLoginAtUtc, u.CreatedAtUtc);

    private async Task<UserAdminDetailResponse> ToDetailAsync(UserAccount u, CancellationToken cancellationToken)
    {
        var projectCount = await db.ProjectMemberships.AsNoTracking().CountAsync(m => m.UserId == u.Id, cancellationToken);
        var now = timeProvider.UtcNow();
        return new UserAdminDetailResponse(
            u.Id, u.Email, u.DisplayName, u.SystemRole, u.IsActive, u.IsLockedAt(now), u.MustChangePassword,
            u.LastLoginAtUtc, u.CreatedAtUtc, u.IsLockedAt(now) ? u.LockedUntilUtc : null, u.TimeZoneId, projectCount, u.RowVersion);
    }
}
