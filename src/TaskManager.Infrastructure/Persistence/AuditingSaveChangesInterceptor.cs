using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TaskManager.Application.Abstractions;
using TaskManager.Domain.Common;

namespace TaskManager.Infrastructure.Persistence;

/// <summary>
/// 保存時に監査列（作成・更新の日時と利用者）を設定し、楽観的排他の版（row_version）を +1 する
/// （architecture.md §3 の 6、database-design.md §1）。
/// </summary>
public sealed class AuditingSaveChangesInterceptor(ICurrentUser currentUser, TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        Guid? userId = currentUser.IsAuthenticated ? currentUser.UserId : null;
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is IAuditable auditable)
            {
                if (entry.State == EntityState.Added)
                {
                    auditable.CreatedAtUtc = now;
                    auditable.CreatedByUserId ??= userId;
                    auditable.UpdatedAtUtc = now;
                    auditable.UpdatedByUserId ??= userId;
                }
                else if (entry.State == EntityState.Modified)
                {
                    auditable.UpdatedAtUtc = now;
                    auditable.UpdatedByUserId = userId ?? auditable.UpdatedByUserId;
                }
            }

            if (entry.Entity is IVersioned versioned && entry.State == EntityState.Modified)
            {
                versioned.RowVersion = unchecked(versioned.RowVersion + 1);
            }
        }
    }
}
