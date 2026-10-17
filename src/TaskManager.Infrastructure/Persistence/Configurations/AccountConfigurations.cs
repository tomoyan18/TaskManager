using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManager.Domain.Accounts;
using TaskManager.Domain.Auditing;

namespace TaskManager.Infrastructure.Persistence.Configurations;

internal sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> b)
    {
        b.ToTable("user_account", t => t.HasCheckConstraint("ck_user_account__system_role", "system_role IN (1, 2)"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Email).HasMaxLength(254).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(UserAccount.DisplayNameMaxLength).IsRequired();
        b.Property(x => x.SystemRole).AsTinyInt().IsRequired();
        b.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
        b.Property(x => x.FailedLoginCount).HasColumnType("tinyint unsigned");
        b.Property(x => x.LockedUntilUtc).HasColumnType(ConfigurationHelpers.DateTime6);
        b.Property(x => x.LastLoginAtUtc).HasColumnType(ConfigurationHelpers.DateTime6);
        b.Property(x => x.TimeZoneId).HasMaxLength(64).IsRequired();
        b.ConfigureRowVersion();
        b.ConfigureAudit("user_account");
        b.HasIndex(x => x.Email).IsUnique().HasDatabaseName("ux_user_account__email");
        b.HasIndex(x => new { x.IsActive, x.DisplayName }).HasDatabaseName("ix_user_account__is_active__display_name");
    }
}

internal sealed class RefreshSessionConfiguration : IEntityTypeConfiguration<RefreshSession>
{
    public void Configure(EntityTypeBuilder<RefreshSession> b)
    {
        b.ToTable("refresh_session");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.TokenHash).HasColumnType("binary(32)").IsRequired();
        b.Property(x => x.CreatedAtUtc).HasColumnType(ConfigurationHelpers.DateTime6);
        b.Property(x => x.ExpiresAtUtc).HasColumnType(ConfigurationHelpers.DateTime6);
        b.Property(x => x.AbsoluteExpiresAtUtc).HasColumnType(ConfigurationHelpers.DateTime6);
        b.Property(x => x.RotatedAtUtc).HasColumnType(ConfigurationHelpers.DateTime6);
        b.Property(x => x.RevokedAtUtc).HasColumnType(ConfigurationHelpers.DateTime6);
        b.Property(x => x.UserAgent).HasMaxLength(RefreshSession.UserAgentMaxLength);
        b.Property(x => x.CreatedFromIp).HasMaxLength(RefreshSession.IpAddressMaxLength);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("ux_refresh_session__token_hash");
        b.HasIndex(x => x.UserId).HasDatabaseName("ix_refresh_session__user_id");
        b.HasIndex(x => x.FamilyId).HasDatabaseName("ix_refresh_session__family_id");
        b.HasIndex(x => x.ExpiresAtUtc).HasDatabaseName("ix_refresh_session__expires_at_utc");
    }
}

internal sealed class SecurityAuditEventConfiguration : IEntityTypeConfiguration<SecurityAuditEvent>
{
    public void Configure(EntityTypeBuilder<SecurityAuditEvent> b)
    {
        b.ToTable("security_audit_event");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Kind).AsTinyInt().IsRequired();
        b.Property(x => x.IpAddress).HasMaxLength(45);
        b.Property(x => x.UserAgent).HasMaxLength(255);
        b.Property(x => x.DetailJson).HasColumnType("json");
        b.Property(x => x.OccurredAtUtc).HasColumnType(ConfigurationHelpers.DateTime6);
        b.HasIndex(x => x.OccurredAtUtc).HasDatabaseName("ix_security_audit_event__occurred_at_utc");
        b.HasIndex(x => new { x.SubjectUserId, x.OccurredAtUtc })
            .HasDatabaseName("ix_security_audit_event__subject_user_id__occurred_at_utc");
    }
}
