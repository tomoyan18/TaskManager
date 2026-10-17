using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "security_audit_event",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    kind = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    subject_user_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    project_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    ip_address = table.Column<string>(type: "varchar(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    detail_json = table.Column<string>(type: "json", nullable: true),
                    occurred_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_security_audit_event", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_account",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    email = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: false),
                    display_name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    system_role = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    password_hash = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    must_change_password = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    failed_login_count = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    locked_until_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    last_login_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    time_zone_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    security_stamp = table.Column<Guid>(type: "char(36)", nullable: false),
                    row_version = table.Column<uint>(type: "int unsigned", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "char(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_account", x => x.id);
                    table.CheckConstraint("ck_user_account__system_role", "system_role IN (1, 2)");
                    table.ForeignKey(
                        name: "fk_user_account__user_account__created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_account__user_account__updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "project",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    project_key = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    description = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    archived_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    last_task_number = table.Column<uint>(type: "int unsigned", nullable: false),
                    row_version = table.Column<uint>(type: "int unsigned", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "char(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project", x => x.id);
                    table.CheckConstraint("ck_project__key_format", "REGEXP_LIKE(project_key, '^[A-Z][A-Z0-9]{1,9}$', 'c')");
                    table.ForeignKey(
                        name: "fk_project__user_account__created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project__user_account__updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "refresh_session",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    user_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    family_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    token_hash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    absolute_expires_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    rotated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    revoked_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    user_agent = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    created_from_ip = table.Column<string>(type: "varchar(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_session", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_session__user_account__user_id",
                        column: x => x.user_id,
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "milestone",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    project_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    name = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    description = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    target_date = table.Column<DateOnly>(type: "date", nullable: true),
                    state = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    row_version = table.Column<uint>(type: "int unsigned", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "char(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_milestone", x => x.id);
                    table.CheckConstraint("ck_milestone__state", "state IN (1, 2)");
                    table.ForeignKey(
                        name: "fk_milestone__project__project_id",
                        column: x => x.project_id,
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_milestone__user_account__created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_milestone__user_account__updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "project_membership",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    project_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    user_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    role = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    joined_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_membership", x => x.id);
                    table.CheckConstraint("ck_project_membership__role", "role BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "fk_project_membership__project__project_id",
                        column: x => x.project_id,
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_project_membership__user_account__created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project_membership__user_account__updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project_membership__user_account__user_id",
                        column: x => x.user_id,
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "tag",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    project_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    name = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    color = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "char(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tag", x => x.id);
                    table.CheckConstraint("ck_tag__color", "color BETWEEN 1 AND 9");
                    table.ForeignKey(
                        name: "fk_tag__project__project_id",
                        column: x => x.project_id,
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tag__user_account__created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tag__user_account__updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "workflow_status",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    project_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    name = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    category = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    color = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    sort_order = table.Column<ushort>(type: "smallint unsigned", nullable: false),
                    is_default = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "char(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_status", x => x.id);
                    table.CheckConstraint("ck_workflow_status__category", "category BETWEEN 1 AND 4");
                    table.CheckConstraint("ck_workflow_status__color", "color BETWEEN 1 AND 9");
                    table.ForeignKey(
                        name: "fk_workflow_status__project__project_id",
                        column: x => x.project_id,
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_workflow_status__user_account__created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_workflow_status__user_account__updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "task_item",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    project_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    task_number = table.Column<uint>(type: "int unsigned", nullable: false),
                    title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "mediumtext", nullable: true),
                    status_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    priority = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    assignee_user_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    milestone_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    parent_task_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    completed_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    row_version = table.Column<uint>(type: "int unsigned", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "char(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_item", x => x.id);
                    table.CheckConstraint("ck_task_item__date_range", "start_date IS NULL OR due_date IS NULL OR start_date <= due_date");
                    table.CheckConstraint("ck_task_item__number", "task_number >= 1");
                    table.CheckConstraint("ck_task_item__priority", "priority BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "fk_task_item__milestone__milestone_id",
                        column: x => x.milestone_id,
                        principalTable: "milestone",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_task_item__project__project_id",
                        column: x => x.project_id,
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_task_item__task_item__parent_task_id",
                        column: x => x.parent_task_id,
                        principalTable: "task_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_task_item__user_account__assignee_user_id",
                        column: x => x.assignee_user_id,
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_item__user_account__created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_item__user_account__updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_item__workflow_status__status_id",
                        column: x => x.status_id,
                        principalTable: "workflow_status",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "notice",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    kind = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    project_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    task_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    message = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    link_path = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    read_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notice", x => x.id);
                    table.ForeignKey(
                        name: "fk_notice__project__project_id",
                        column: x => x.project_id,
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_notice__task_item__task_id",
                        column: x => x.task_id,
                        principalTable: "task_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_notice__user_account__actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notice__user_account__recipient_user_id",
                        column: x => x.recipient_user_id,
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "task_attachment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    task_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    original_file_name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    storage_key = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    sha256 = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    uploaded_by_user_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    uploaded_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_attachment", x => x.id);
                    table.CheckConstraint("ck_task_attachment__size", "size_bytes BETWEEN 1 AND 10485760");
                    table.ForeignKey(
                        name: "fk_task_attachment__task_item__task_id",
                        column: x => x.task_id,
                        principalTable: "task_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_task_attachment__user_account__uploaded_by_user_id",
                        column: x => x.uploaded_by_user_id,
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "task_comment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    task_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    author_user_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    edited_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_comment", x => x.id);
                    table.ForeignKey(
                        name: "fk_task_comment__task_item__task_id",
                        column: x => x.task_id,
                        principalTable: "task_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_task_comment__user_account__author_user_id",
                        column: x => x.author_user_id,
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "task_history_entry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    task_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    kind = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    old_value = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    new_value = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    occurred_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_history_entry", x => x.id);
                    table.ForeignKey(
                        name: "fk_task_history_entry__task_item__task_id",
                        column: x => x.task_id,
                        principalTable: "task_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_task_history_entry__user_account__actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "task_item_tag",
                columns: table => new
                {
                    task_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    tag_id = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_item_tag", x => new { x.task_id, x.tag_id });
                    table.ForeignKey(
                        name: "fk_task_item_tag__tag__tag_id",
                        column: x => x.tag_id,
                        principalTable: "tag",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_task_item_tag__task_item__task_id",
                        column: x => x.task_id,
                        principalTable: "task_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_milestone__created_by_user_id",
                table: "milestone",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_milestone__project_id__state__target_date",
                table: "milestone",
                columns: new[] { "project_id", "state", "target_date" });

            migrationBuilder.CreateIndex(
                name: "ix_milestone__updated_by_user_id",
                table: "milestone",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_milestone__project_id__name",
                table: "milestone",
                columns: new[] { "project_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notice__actor_user_id",
                table: "notice",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_notice__created_at_utc",
                table: "notice",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_notice__project_id",
                table: "notice",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_notice__recipient_user_id__read_at_utc__created_at_utc",
                table: "notice",
                columns: new[] { "recipient_user_id", "read_at_utc", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_notice__task_id",
                table: "notice",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "ix_project__archived_at_utc",
                table: "project",
                column: "archived_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_project__created_by_user_id",
                table: "project",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_project__updated_by_user_id",
                table: "project",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_project__name",
                table: "project",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_project__project_key",
                table: "project",
                column: "project_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_project_membership__created_by_user_id",
                table: "project_membership",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_membership__updated_by_user_id",
                table: "project_membership",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_membership__user_id",
                table: "project_membership",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_project_membership__project_id__user_id",
                table: "project_membership",
                columns: new[] { "project_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_session__expires_at_utc",
                table: "refresh_session",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_session__family_id",
                table: "refresh_session",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_session__user_id",
                table: "refresh_session",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_refresh_session__token_hash",
                table: "refresh_session",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_security_audit_event__occurred_at_utc",
                table: "security_audit_event",
                column: "occurred_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_security_audit_event__subject_user_id__occurred_at_utc",
                table: "security_audit_event",
                columns: new[] { "subject_user_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_tag__created_by_user_id",
                table: "tag",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_tag__updated_by_user_id",
                table: "tag",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_tag__project_id__name",
                table: "tag",
                columns: new[] { "project_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_task_attachment__task_id",
                table: "task_attachment",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_attachment__uploaded_by_user_id",
                table: "task_attachment",
                column: "uploaded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_task_attachment__storage_key",
                table: "task_attachment",
                column: "storage_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_task_comment__author_user_id",
                table: "task_comment",
                column: "author_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_comment__task_id__created_at_utc",
                table: "task_comment",
                columns: new[] { "task_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_task_history_entry__actor_user_id",
                table: "task_history_entry",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_history_entry__task_id__occurred_at_utc",
                table: "task_history_entry",
                columns: new[] { "task_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_task_item__assignee_user_id__due_date",
                table: "task_item",
                columns: new[] { "assignee_user_id", "due_date" });

            migrationBuilder.CreateIndex(
                name: "ix_task_item__created_by_user_id",
                table: "task_item",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_item__milestone_id",
                table: "task_item",
                column: "milestone_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_item__parent_task_id",
                table: "task_item",
                column: "parent_task_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_item__project_id__assignee_user_id",
                table: "task_item",
                columns: new[] { "project_id", "assignee_user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_task_item__project_id__due_date",
                table: "task_item",
                columns: new[] { "project_id", "due_date" });

            migrationBuilder.CreateIndex(
                name: "ix_task_item__project_id__status_id",
                table: "task_item",
                columns: new[] { "project_id", "status_id" });

            migrationBuilder.CreateIndex(
                name: "ix_task_item__project_id__updated_at_utc",
                table: "task_item",
                columns: new[] { "project_id", "updated_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_task_item__status_id",
                table: "task_item",
                column: "status_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_item__updated_by_user_id",
                table: "task_item",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_task_item__project_id__task_number",
                table: "task_item",
                columns: new[] { "project_id", "task_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_task_item_tag__tag_id",
                table: "task_item_tag",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_account__created_by_user_id",
                table: "user_account",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_account__is_active__display_name",
                table: "user_account",
                columns: new[] { "is_active", "display_name" });

            migrationBuilder.CreateIndex(
                name: "ix_user_account__updated_by_user_id",
                table: "user_account",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_user_account__email",
                table: "user_account",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workflow_status__created_by_user_id",
                table: "workflow_status",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_status__project_id__sort_order",
                table: "workflow_status",
                columns: new[] { "project_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_workflow_status__updated_by_user_id",
                table: "workflow_status",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_workflow_status__project_id__name",
                table: "workflow_status",
                columns: new[] { "project_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notice");

            migrationBuilder.DropTable(
                name: "project_membership");

            migrationBuilder.DropTable(
                name: "refresh_session");

            migrationBuilder.DropTable(
                name: "security_audit_event");

            migrationBuilder.DropTable(
                name: "task_attachment");

            migrationBuilder.DropTable(
                name: "task_comment");

            migrationBuilder.DropTable(
                name: "task_history_entry");

            migrationBuilder.DropTable(
                name: "task_item_tag");

            migrationBuilder.DropTable(
                name: "tag");

            migrationBuilder.DropTable(
                name: "task_item");

            migrationBuilder.DropTable(
                name: "milestone");

            migrationBuilder.DropTable(
                name: "workflow_status");

            migrationBuilder.DropTable(
                name: "project");

            migrationBuilder.DropTable(
                name: "user_account");
        }
    }
}
