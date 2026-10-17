using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using TaskManager.Testing;

[assembly: AssemblyFixture(typeof(DatabaseFixture))]

namespace TaskManager.Infrastructure.Tests.Persistence;

/// <summary>マイグレーション適用後のスキーマが database-design.md と一致すること。</summary>
public class SchemaTests(DatabaseFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<List<T>> QueryAsync<T>(string sql, Func<MySqlDataReader, T> map)
    {
        await using var connection = new MySqlConnection(fixture.Database.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // テスト内の固定 SQL
        command.CommandText = sql;
#pragma warning restore CA2100
        command.Parameters.AddWithValue("@schema", fixture.Database.Name);
        await using var reader = (MySqlDataReader)await command.ExecuteReaderAsync(Ct);
        var rows = new List<T>();
        while (await reader.ReadAsync(Ct))
        {
            rows.Add(map(reader));
        }

        return rows;
    }

    [Fact]
    public async Task マイグレーションがすべて適用されモデルとの差分がない()
    {
        await using var db = fixture.Database.CreateDbContext();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(Ct));
        Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync(Ct));
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task 設計どおりの14表がある()
    {
        var tables = await QueryAsync(
            "SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = @schema AND TABLE_NAME <> '__EFMigrationsHistory' ORDER BY TABLE_NAME",
            r => r.GetString(0));

        Assert.Equal(
            ["milestone", "notice", "project", "project_membership", "refresh_session", "security_audit_event", "tag",
             "task_attachment", "task_comment", "task_history_entry", "task_item", "task_item_tag", "user_account", "workflow_status"],
            tables);
    }

    [Fact]
    public async Task 文字コードはutf8mb4()
    {
        var collations = await QueryAsync(
            "SELECT DISTINCT TABLE_COLLATION FROM information_schema.TABLES WHERE TABLE_SCHEMA = @schema AND TABLE_NAME <> '__EFMigrationsHistory'",
            r => r.GetString(0));
        Assert.All(collations, c => Assert.StartsWith("utf8mb4", c, StringComparison.Ordinal));
    }

    [Fact]
    public async Task CHECK制約が11件ある()
    {
        var checks = await QueryAsync(
            "SELECT CONSTRAINT_NAME FROM information_schema.TABLE_CONSTRAINTS WHERE TABLE_SCHEMA = @schema AND CONSTRAINT_TYPE = 'CHECK' ORDER BY CONSTRAINT_NAME",
            r => r.GetString(0));
        Assert.Equal(
            ["ck_milestone__state", "ck_project__key_format", "ck_project_membership__role", "ck_tag__color", "ck_task_attachment__size",
             "ck_task_item__date_range", "ck_task_item__number", "ck_task_item__priority", "ck_user_account__system_role",
             "ck_workflow_status__category", "ck_workflow_status__color"],
            checks);
    }

    [Theory]
    [InlineData("user_account", "email", "ux_user_account__email")]
    [InlineData("project", "project_key", "ux_project__project_key")]
    [InlineData("task_item", "project_id,task_number", "ux_task_item__project_id__task_number")]
    [InlineData("project_membership", "project_id,user_id", "ux_project_membership__project_id__user_id")]
    [InlineData("refresh_session", "token_hash", "ux_refresh_session__token_hash")]
    public async Task 一意制約がある(string table, string columns, string indexName)
    {
        var rows = await QueryAsync(
            $"SELECT GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = '{table}' AND INDEX_NAME = '{indexName}' AND NON_UNIQUE = 0",
            r => r.IsDBNull(0) ? null : r.GetString(0));
        Assert.Equal(columns, Assert.Single(rows));
    }

    [Theory]
    [InlineData("ix_task_item__assignee_user_id__project_id__status_id", "assignee_user_id,project_id,status_id")] // Phase 9
    [InlineData("ix_task_item__project_id__status_id", "project_id,status_id")]
    [InlineData("ix_task_item__project_id__due_date", "project_id,due_date")]
    public async Task ガントと円グラフの集計に使う索引がある(string indexName, string columns)
    {
        var rows = await QueryAsync(
            $"SELECT GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = 'task_item' AND INDEX_NAME = '{indexName}' AND NON_UNIQUE = 1",
            r => r.IsDBNull(0) ? null : r.GetString(0));
        Assert.Equal(columns, Assert.Single(rows));
    }

    [Fact]
    public async Task マイグレーションは追加順に適用されている()
    {
        await using var db = fixture.Database.CreateDbContext();
        var applied = (await db.Database.GetAppliedMigrationsAsync(Ct)).ToList();

        Assert.Equal(2, applied.Count);
        Assert.EndsWith("_InitialSchema", applied[0], StringComparison.Ordinal);
        Assert.EndsWith("_AddTaskStatusSummaryIndex", applied[1], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("task_item", "workflow_status", "RESTRICT")]
    [InlineData("task_item", "milestone", "SET NULL")]
    [InlineData("task_item_tag", "task_item", "CASCADE")]
    [InlineData("task_item", "task_item", "CASCADE")]
    [InlineData("project_membership", "user_account", "RESTRICT")]
    public async Task 外部キーの削除時の挙動(string table, string referenced, string deleteRule)
    {
        var rules = await QueryAsync(
            $"SELECT DELETE_RULE FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = @schema AND TABLE_NAME = '{table}' AND REFERENCED_TABLE_NAME = '{referenced}'",
            r => r.GetString(0));
        Assert.Contains(deleteRule, rules);
    }

    [Fact]
    public async Task 日時列はDATETIME6で日付列はDATE()
    {
        var types = await QueryAsync(
            "SELECT COLUMN_NAME, COLUMN_TYPE FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = 'task_item' AND COLUMN_NAME IN ('due_date', 'start_date', 'completed_at_utc', 'created_at_utc')",
            r => (r.GetString(0), r.GetString(1)));
        Assert.Contains(("due_date", "date"), types);
        Assert.Contains(("start_date", "date"), types);
        Assert.Contains(("completed_at_utc", "datetime(6)"), types);
        Assert.Contains(("created_at_utc", "datetime(6)"), types);
    }
}
