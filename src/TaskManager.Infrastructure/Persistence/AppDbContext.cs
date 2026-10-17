using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using TaskManager.Application.Abstractions;
using TaskManager.Domain.Accounts;
using TaskManager.Domain.Auditing;
using TaskManager.Domain.Common;
using TaskManager.Domain.Notices;
using TaskManager.Domain.Projects;
using TaskManager.Domain.Tasks;

namespace TaskManager.Infrastructure.Persistence;

/// <summary>TaskManager の EF Core コンテキスト（MySQL。database-design.md）。</summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectMembership> ProjectMemberships => Set<ProjectMembership>();

    public DbSet<WorkflowStatus> WorkflowStatuses => Set<WorkflowStatus>();

    public DbSet<Tag> Tags => Set<Tag>();

    public DbSet<Milestone> Milestones => Set<Milestone>();

    public DbSet<TaskItem> TaskItems => Set<TaskItem>();

    public DbSet<TaskItemTag> TaskItemTags => Set<TaskItemTag>();

    public DbSet<TaskComment> TaskComments => Set<TaskComment>();

    public DbSet<TaskAttachment> TaskAttachments => Set<TaskAttachment>();

    public DbSet<TaskHistoryEntry> TaskHistoryEntries => Set<TaskHistoryEntry>();

    public DbSet<Notice> Notices => Set<Notice>();

    public DbSet<SecurityAuditEvent> SecurityAuditEvents => Set<SecurityAuditEvent>();

    public async Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (Database.CurrentTransaction is not null)
        {
            return await action(cancellationToken);
        }

        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
        var result = await action(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public void SetExpectedVersion<TEntity>(TEntity entity, uint expectedVersion)
        where TEntity : class, IVersioned =>
        Entry(entity).Property(nameof(IVersioned.RowVersion)).OriginalValue = expectedVersion;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // MySQL の DATETIME はタイムゾーンを持たないため、読み込んだ値を UTC として扱う（列名 *_at_utc）
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();

        // MySql.EntityFrameworkCore 10.0.9 は DATE 列を DateTime として読み込み、DateOnly に変換できない
        // （Phase 5 で検出）。DateTime として受け渡し、値は日付部分だけを使う
        configurationBuilder.Properties<DateOnly>().HaveConversion<DateOnlyConverter>().HaveColumnType("date");
        configurationBuilder.Properties<DateOnly?>().HaveConversion<NullableDateOnlyConverter>().HaveColumnType("date");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        ApplyNamingConventions(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// 表名・列名・主キー名・外部キー名を naming-rules.md §4 の規則に揃える
    /// （明示的に指定したものはそのまま）。外部ライブラリは使わない。
    /// </summary>
    private static void ApplyNamingConventions(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var table = entity.GetTableName() ?? ToSnakeCase(entity.ClrType.Name);
            foreach (var property in entity.GetProperties())
            {
                if (property.FindAnnotation(RelationalAnnotationNames.ColumnName) is null)
                {
                    property.SetColumnName(ToSnakeCase(property.Name));
                }
            }

            entity.FindPrimaryKey()?.SetName($"pk_{table}");
            foreach (var fk in entity.GetForeignKeys())
            {
                if (fk.FindAnnotation(RelationalAnnotationNames.Name) is null)
                {
                    var principal = fk.PrincipalEntityType.GetTableName();
                    var columns = string.Join("__", fk.Properties.Select(p => p.GetColumnName()));
                    fk.SetConstraintName($"fk_{table}__{principal}__{columns}");
                }
            }

            foreach (var index in entity.GetIndexes())
            {
                if (index.FindAnnotation(RelationalAnnotationNames.Name) is null)
                {
                    var prefix = index.IsUnique ? "ux" : "ix";
                    index.SetDatabaseName($"{prefix}_{table}__{string.Join("__", index.Properties.Select(p => p.GetColumnName()))}");
                }
            }
        }
    }

    internal static string ToSnakeCase(string name)
    {
        var builder = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1])
                    || (i + 1 < name.Length && char.IsLower(name[i + 1]) && char.IsUpper(name[i - 1]))))
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
