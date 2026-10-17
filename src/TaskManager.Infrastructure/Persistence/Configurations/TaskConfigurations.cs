using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManager.Domain.Accounts;
using TaskManager.Domain.Notices;
using TaskManager.Domain.Projects;
using TaskManager.Domain.Tasks;

namespace TaskManager.Infrastructure.Persistence.Configurations;

internal sealed class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> b)
    {
        b.ToTable("task_item", t =>
        {
            t.HasCheckConstraint("ck_task_item__date_range", "start_date IS NULL OR due_date IS NULL OR start_date <= due_date");
            t.HasCheckConstraint("ck_task_item__priority", "priority BETWEEN 1 AND 4");
            t.HasCheckConstraint("ck_task_item__number", "task_number >= 1");
            // parent_task_id は ON DELETE CASCADE のため CHECK 制約で参照できない（MySQL エラー 3823）。自己参照の禁止はドメイン規則 TK-03 で保証する
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Number).HasColumnName("task_number").HasColumnType("int unsigned");
        b.Property(x => x.Title).HasMaxLength(TaskItem.TitleMaxLength).IsRequired();
        b.Property(x => x.Description).HasColumnType("mediumtext");
        b.Property(x => x.Priority).AsTinyInt().IsRequired();
        b.Property(x => x.StartDate).HasColumnType("date");
        b.Property(x => x.DueDate).HasColumnType("date");
        b.Property(x => x.CompletedAtUtc).HasColumnType(ConfigurationHelpers.DateTime6);
        b.ConfigureRowVersion();
        b.ConfigureAudit("task_item");

        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<WorkflowStatus>().WithMany().HasForeignKey(x => x.StatusId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.AssigneeUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Milestone>().WithMany().HasForeignKey(x => x.MilestoneId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<TaskItem>().WithMany().HasForeignKey(x => x.ParentTaskId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Tags).WithOne().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Tags).UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(x => new { x.ProjectId, x.Number }).IsUnique().HasDatabaseName("ux_task_item__project_id__task_number");
        b.HasIndex(x => new { x.ProjectId, x.StatusId }).HasDatabaseName("ix_task_item__project_id__status_id");
        b.HasIndex(x => new { x.ProjectId, x.AssigneeUserId }).HasDatabaseName("ix_task_item__project_id__assignee_user_id");
        b.HasIndex(x => new { x.AssigneeUserId, x.DueDate }).HasDatabaseName("ix_task_item__assignee_user_id__due_date");
        // 自分の担当のステータス別集計（Phase 9。database-design.md §11）
        b.HasIndex(x => new { x.AssigneeUserId, x.ProjectId, x.StatusId }).HasDatabaseName("ix_task_item__assignee_user_id__project_id__status_id");
        b.HasIndex(x => new { x.ProjectId, x.DueDate }).HasDatabaseName("ix_task_item__project_id__due_date");
        b.HasIndex(x => new { x.ProjectId, x.UpdatedAtUtc }).HasDatabaseName("ix_task_item__project_id__updated_at_utc");
        b.HasIndex(x => x.ParentTaskId).HasDatabaseName("ix_task_item__parent_task_id");
        b.HasIndex(x => x.MilestoneId).HasDatabaseName("ix_task_item__milestone_id");
        b.HasIndex(x => x.StatusId).HasDatabaseName("ix_task_item__status_id");
    }
}

internal sealed class TaskItemTagConfiguration : IEntityTypeConfiguration<TaskItemTag>
{
    public void Configure(EntityTypeBuilder<TaskItemTag> b)
    {
        b.ToTable("task_item_tag");
        b.HasKey(x => new { x.TaskId, x.TagId });
        b.HasOne<Tag>().WithMany().HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.TagId).HasDatabaseName("ix_task_item_tag__tag_id");
    }
}

internal sealed class TaskCommentConfiguration : IEntityTypeConfiguration<TaskComment>
{
    public void Configure(EntityTypeBuilder<TaskComment> b)
    {
        b.ToTable("task_comment");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Body).HasColumnType("text").IsRequired();
        b.Property(x => x.CreatedAtUtc).HasColumnType(ConfigurationHelpers.DateTime6);
        b.Property(x => x.EditedAtUtc).HasColumnType(ConfigurationHelpers.DateTime6);
        b.HasOne<TaskItem>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TaskId, x.CreatedAtUtc }).HasDatabaseName("ix_task_comment__task_id__created_at_utc");
    }
}

internal sealed class TaskAttachmentConfiguration : IEntityTypeConfiguration<TaskAttachment>
{
    public void Configure(EntityTypeBuilder<TaskAttachment> b)
    {
        b.ToTable("task_attachment", t => t.HasCheckConstraint("ck_task_attachment__size", "size_bytes BETWEEN 1 AND 10485760"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.OriginalFileName).HasMaxLength(TaskAttachment.FileNameMaxLength).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        b.Property(x => x.SizeBytes).HasColumnType("bigint unsigned");
        b.Property(x => x.StorageKey).HasMaxLength(64).IsRequired();
        b.Property(x => x.Sha256).HasColumnName("sha256").HasColumnType("binary(32)").IsRequired();
        b.Property(x => x.UploadedAtUtc).HasColumnType(ConfigurationHelpers.DateTime6);
        b.HasOne<TaskItem>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.StorageKey).IsUnique().HasDatabaseName("ux_task_attachment__storage_key");
        b.HasIndex(x => x.TaskId).HasDatabaseName("ix_task_attachment__task_id");
    }
}

internal sealed class TaskHistoryEntryConfiguration : IEntityTypeConfiguration<TaskHistoryEntry>
{
    public void Configure(EntityTypeBuilder<TaskHistoryEntry> b)
    {
        b.ToTable("task_history_entry");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Kind).AsTinyInt().IsRequired();
        b.Property(x => x.OldValue).HasMaxLength(TaskHistoryEntry.ValueMaxLength);
        b.Property(x => x.NewValue).HasMaxLength(TaskHistoryEntry.ValueMaxLength);
        b.Property(x => x.OccurredAtUtc).HasColumnType(ConfigurationHelpers.DateTime6);
        b.HasOne<TaskItem>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TaskId, x.OccurredAtUtc }).HasDatabaseName("ix_task_history_entry__task_id__occurred_at_utc");
    }
}

internal sealed class NoticeConfiguration : IEntityTypeConfiguration<Notice>
{
    public void Configure(EntityTypeBuilder<Notice> b)
    {
        b.ToTable("notice");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Kind).AsTinyInt().IsRequired();
        b.Property(x => x.Message).HasMaxLength(Notice.MessageMaxLength).IsRequired();
        b.Property(x => x.LinkPath).HasMaxLength(Notice.LinkPathMaxLength);
        b.Property(x => x.ReadAtUtc).HasColumnType(ConfigurationHelpers.DateTime6);
        b.Property(x => x.CreatedAtUtc).HasColumnType(ConfigurationHelpers.DateTime6);
        b.Ignore(x => x.IsRead);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.RecipientUserId).OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_notice__user_account__recipient_user_id");
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_notice__user_account__actor_user_id");
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<TaskItem>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => new { x.RecipientUserId, x.ReadAtUtc, x.CreatedAtUtc })
            .HasDatabaseName("ix_notice__recipient_user_id__read_at_utc__created_at_utc");
        b.HasIndex(x => x.CreatedAtUtc).HasDatabaseName("ix_notice__created_at_utc");
        b.HasIndex(x => x.TaskId).HasDatabaseName("ix_notice__task_id");
    }
}
