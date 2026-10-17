using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Common;
using TaskManager.Domain.Common;

namespace TaskManager.Application.Users;

/// <summary>メンバー追加用の利用者検索（REQ-USR-08）。いずれかのプロジェクトの管理者、またはシステム管理者のみ。</summary>
public sealed class UserDirectoryService(IAppDbContext db, ICurrentUser currentUser)
{
    public const int MaxResults = 20;

    public async Task<IReadOnlyList<UserDirectoryEntry>> SearchAsync(
        string q, string? excludeProjectKey, CancellationToken cancellationToken)
    {
        if (!currentUser.IsSystemAdministrator)
        {
            var isOwner = await db.ProjectMemberships.AsNoTracking()
                .AnyAsync(m => m.UserId == currentUser.UserId && m.Role == ProjectRole.Owner, cancellationToken);
            if (!isOwner)
            {
                throw Errors.Forbidden();
            }
        }

        var term = q.Trim();
        var query = db.UserAccounts.AsNoTracking()
            .Where(u => u.IsActive && (u.DisplayName.Contains(term) || u.Email.Contains(term)));

        if (!string.IsNullOrWhiteSpace(excludeProjectKey) && ProjectKey.TryCreate(excludeProjectKey, out var key))
        {
            var projectId = await db.Projects.AsNoTracking().Where(p => p.Key == key.Value).Select(p => (Guid?)p.Id)
                .SingleOrDefaultAsync(cancellationToken);
            if (projectId is { } pid)
            {
                query = query.Where(u => !db.ProjectMemberships.Any(m => m.ProjectId == pid && m.UserId == u.Id));
            }
        }

        return await query.OrderBy(u => u.DisplayName).Take(MaxResults)
            .Select(u => new UserDirectoryEntry(u.Id, u.DisplayName, u.Email))
            .ToListAsync(cancellationToken);
    }
}
