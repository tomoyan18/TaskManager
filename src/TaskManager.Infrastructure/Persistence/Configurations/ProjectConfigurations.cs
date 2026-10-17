using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManager.Domain.Accounts;
using TaskManager.Domain.Projects;

namespace TaskManager.Infrastructure.Persistence.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> b)
    {
        b.ToTable("project", t => t.HasCheckConstraint(
            "ck_project__key_format", "REGEXP_LIKE(project_key, '^[A-Z][A-Z0-9]{1,9}$', 'c')"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Key).HasColumnName("project_key").HasMaxLength(10).IsRequired();
        b.Property(x => x.Name).HasMaxLength(Project.NameMaxLength).IsRequired();
        b.Property(x => x.Description).HasMaxLength(Project.DescriptionMaxLength);
        b.Property(x => x.ArchivedAtUtc).HasColumnType(ConfigurationHelpers.DateTime6);
        b.Property(x => x.LastTaskNumber).IsRequired();
        b.Ignore(x => x.IsArchived);
        b.ConfigureRowVersion();
        b.ConfigureAudit("project");
        b.HasIndex(x => x.Key).IsUnique().HasDatabaseName("ux_project__project_key");
        b.HasIndex(x => x.Name).IsUnique().HasDatabaseName("ux_project__name");
        b.HasIndex(x => x.ArchivedAtUtc).HasDatabaseName("ix_project__archived_at_utc");
    }
}

internal sealed class ProjectMembershipConfiguration : IEntityTypeConfiguration<ProjectMembership>
{
    public void Configure(EntityTypeBuilder<ProjectMembership> b)
    {
        b.ToTable("project_membership", t => t.HasCheckConstraint("ck_project_membership__role", "role BETWEEN 1 AND 3"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Role).AsTinyInt().IsRequired();
        b.Property(x => x.JoinedAtUtc).HasColumnType(ConfigurationHelpers.DateTime6);
        b.Property(x => x.UpdatedAtUtc).HasColumnType(ConfigurationHelpers.DateTime6);
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_project_membership__user_account__created_by_user_id");
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_project_membership__user_account__updated_by_user_id");
        b.HasIndex(x => new { x.ProjectId, x.UserId }).IsUnique().HasDatabaseName("ux_project_membership__project_id__user_id");
        b.HasIndex(x => x.UserId).HasDatabaseName("ix_project_membership__user_id");
    }
}

internal sealed class WorkflowStatusConfiguration : IEntityTypeConfiguration<WorkflowStatus>
{
    public void Configure(EntityTypeBuilder<WorkflowStatus> b)
    {
        b.ToTable("workflow_status", t =>
        {
            t.HasCheckConstraint("ck_workflow_status__category", "category BETWEEN 1 AND 4");
            t.HasCheckConstraint("ck_workflow_status__color", "color BETWEEN 1 AND 9");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Name).HasMaxLength(WorkflowStatus.NameMaxLength).IsRequired();
        b.Property(x => x.Category).AsTinyInt().IsRequired();
        b.Property(x => x.Color).AsTinyInt().IsRequired();
        b.Property(x => x.SortOrder).HasColumnType("smallint unsigned");
        b.ConfigureAudit("workflow_status");
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ProjectId, x.Name }).IsUnique().HasDatabaseName("ux_workflow_status__project_id__name");
        b.HasIndex(x => new { x.ProjectId, x.SortOrder }).HasDatabaseName("ix_workflow_status__project_id__sort_order");
    }
}

internal sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> b)
    {
        b.ToTable("tag", t => t.HasCheckConstraint("ck_tag__color", "color BETWEEN 1 AND 9"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Name).HasMaxLength(Tag.NameMaxLength).IsRequired();
        b.Property(x => x.Color).AsTinyInt().IsRequired();
        b.ConfigureAudit("tag");
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ProjectId, x.Name }).IsUnique().HasDatabaseName("ux_tag__project_id__name");
    }
}

internal sealed class MilestoneConfiguration : IEntityTypeConfiguration<Milestone>
{
    public void Configure(EntityTypeBuilder<Milestone> b)
    {
        b.ToTable("milestone", t => t.HasCheckConstraint("ck_milestone__state", "state IN (1, 2)"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Name).HasMaxLength(Milestone.NameMaxLength).IsRequired();
        b.Property(x => x.Description).HasMaxLength(Milestone.DescriptionMaxLength);
        b.Property(x => x.TargetDate).HasColumnType("date");
        b.Property(x => x.State).AsTinyInt().IsRequired();
        b.ConfigureRowVersion();
        b.ConfigureAudit("milestone");
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ProjectId, x.Name }).IsUnique().HasDatabaseName("ux_milestone__project_id__name");
        b.HasIndex(x => new { x.ProjectId, x.State, x.TargetDate }).HasDatabaseName("ix_milestone__project_id__state__target_date");
    }
}
