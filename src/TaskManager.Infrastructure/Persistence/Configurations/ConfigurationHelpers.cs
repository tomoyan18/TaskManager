using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManager.Domain.Accounts;
using TaskManager.Domain.Common;

namespace TaskManager.Infrastructure.Persistence.Configurations;

internal static class ConfigurationHelpers
{
    public const string DateTime6 = "datetime(6)";

    /// <summary>監査列（created_at_utc 等）と、利用者への外部キー（RESTRICT）。</summary>
    public static void ConfigureAudit<T>(this EntityTypeBuilder<T> b, string table)
        where T : class, IAuditable
    {
        b.Property(x => x.CreatedAtUtc).HasColumnType(DateTime6).IsRequired();
        b.Property(x => x.UpdatedAtUtc).HasColumnType(DateTime6).IsRequired();
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName($"fk_{table}__user_account__created_by_user_id");
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName($"fk_{table}__user_account__updated_by_user_id");
    }

    public static void ConfigureRowVersion<T>(this EntityTypeBuilder<T> b)
        where T : class, IVersioned =>
        b.Property(x => x.RowVersion).HasColumnName("row_version").IsConcurrencyToken().IsRequired();

    public static PropertyBuilder<TEnum> AsTinyInt<TEnum>(this PropertyBuilder<TEnum> p)
        where TEnum : struct, Enum => p.HasConversion<byte>().HasColumnType("tinyint unsigned");

    public static PropertyBuilder<TEnum?> AsTinyInt<TEnum>(this PropertyBuilder<TEnum?> p)
        where TEnum : struct, Enum => p.HasConversion<byte?>().HasColumnType("tinyint unsigned");
}
