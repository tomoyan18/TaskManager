using Microsoft.EntityFrameworkCore;
using TaskManager.Domain.Accounts;
using TaskManager.Domain.Auditing;
using TaskManager.Domain.Notices;
using TaskManager.Domain.Projects;
using TaskManager.Domain.Tasks;

namespace TaskManager.Application.Abstractions;

/// <summary>
/// Application 層が永続化に使う DB コンテキストの抽象。
/// Repository は設けず、DbSet を直接公開する（docs/design/architecture.md §2「Repository を作らない理由」）。
/// </summary>
public interface IAppDbContext
{
    DbSet<UserAccount> UserAccounts { get; }

    DbSet<RefreshSession> RefreshSessions { get; }

    DbSet<Project> Projects { get; }

    DbSet<ProjectMembership> ProjectMemberships { get; }

    DbSet<WorkflowStatus> WorkflowStatuses { get; }

    DbSet<Tag> Tags { get; }

    DbSet<Milestone> Milestones { get; }

    DbSet<TaskItem> TaskItems { get; }

    DbSet<TaskItemTag> TaskItemTags { get; }

    DbSet<TaskComment> TaskComments { get; }

    DbSet<TaskAttachment> TaskAttachments { get; }

    DbSet<TaskHistoryEntry> TaskHistoryEntries { get; }

    DbSet<Notice> Notices { get; }

    DbSet<SecurityAuditEvent> SecurityAuditEvents { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 1 つのトランザクションで処理する（採番やプロジェクト削除など複数の書き込みをまとめる場合）。
    /// </summary>
    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken);

    /// <summary>楽観的排他: クライアントが送った版を、更新時の比較対象（元の値）として設定する。</summary>
    void SetExpectedVersion<TEntity>(TEntity entity, uint expectedVersion)
        where TEntity : class, Domain.Common.IVersioned;
}
