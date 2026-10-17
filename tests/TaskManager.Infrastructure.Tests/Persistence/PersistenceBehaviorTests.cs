using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;
using TaskManager.Application.Abstractions;
using TaskManager.Domain.Common;
using TaskManager.Domain.Projects;
using TaskManager.Domain.Tasks;
using TaskManager.Infrastructure.Persistence;
using TaskManager.Testing;

namespace TaskManager.Infrastructure.Tests.Persistence;

/// <summary>EF Core + MySQL の実際の動作（採番、楽観的排他、制約、型変換、監査列）。</summary>
public class PersistenceBehaviorTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private ApplicationTestHost _host = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _host = new ApplicationTestHost(fixture.Database);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _host.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private async Task<int?> AllocateInNewScopeAsync(Guid projectId)
    {
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var allocator = scope.ServiceProvider.GetRequiredService<ITaskNumberAllocator>();
        return await db.InTransactionAsync(ct => allocator.AllocateAsync(projectId, ct), Ct);
    }

    [Fact]
    public async Task 採番_20並列でも重複と欠番がなくプロジェクトごとに独立する()
    {
        var owner = await _host.Data.CreateUserAsync();
        var p1 = await _host.Data.CreateProjectAsync(owner);
        var p2 = await _host.Data.CreateProjectAsync(owner);

        var numbers = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => AllocateInNewScopeAsync(p1.Id)));
        var other = await AllocateInNewScopeAsync(p2.Id);

        Assert.Equal(Enumerable.Range(1, 20), numbers.Select(n => n!.Value).Order());
        Assert.Equal(1, other);
    }

    [Fact]
    public async Task 採番_アーカイブ中はnullでトランザクション外は例外()
    {
        var owner = await _host.Data.CreateUserAsync();
        var project = await _host.Data.CreateProjectAsync(owner);
        await _host.Data.ArchiveProjectAsync(project);

        Assert.Null(await AllocateInNewScopeAsync(project.Id));

        await using var scope = _host.Services.CreateAsyncScope();
        var allocator = scope.ServiceProvider.GetRequiredService<ITaskNumberAllocator>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => allocator.AllocateAsync(project.Id, Ct));
    }

    [Fact]
    public async Task 採番_トランザクションを取り消すと番号も戻る()
    {
        var owner = await _host.Data.CreateUserAsync();
        var project = await _host.Data.CreateProjectAsync(owner);

        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var allocator = scope.ServiceProvider.GetRequiredService<ITaskNumberAllocator>();
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.InTransactionAsync<int>(async ct =>
            {
                await allocator.AllocateAsync(project.Id, ct);
                throw new InvalidOperationException("途中で失敗");
            }, Ct));
        }

        Assert.Equal(1, await AllocateInNewScopeAsync(project.Id));
    }

    [Fact]
    public async Task 楽観的排他_同じ版からの2回目の保存は失敗する()
    {
        var owner = await _host.Data.CreateUserAsync();
        var project = await _host.Data.CreateProjectAsync(owner);
        var task = await _host.Data.CreateTaskAsync(project, owner);

        await using var scope1 = _host.Services.CreateAsyncScope();
        await using var scope2 = _host.Services.CreateAsyncScope();
        var db1 = scope1.ServiceProvider.GetRequiredService<AppDbContext>();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var t1 = await db1.TaskItems.SingleAsync(t => t.Id == task.Id, Ct);
        var t2 = await db2.TaskItems.SingleAsync(t => t.Id == task.Id, Ct);
        db1.SetExpectedVersion(t1, t1.RowVersion);
        db2.SetExpectedVersion(t2, t2.RowVersion);

        t1.ChangeTitle("1人目の変更");
        await db1.SaveChangesAsync(Ct);
        t2.ChangeTitle("2人目の変更");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db2.SaveChangesAsync(Ct));

        var saved = await _host.Data.QueryAsync(db => db.TaskItems.AsNoTracking().SingleAsync(t => t.Id == task.Id, Ct));
        Assert.Equal("1人目の変更", saved.Title);
        Assert.Equal(task.RowVersion + 1, saved.RowVersion);
    }

    [Fact]
    public async Task 監査列_作成者と更新者と日時を設定する()
    {
        var owner = await _host.Data.CreateUserAsync();
        var project = await _host.Data.CreateProjectAsync(owner);
        var tag = await _host.Data.CreateTagAsync(project);

        var saved = await _host.Data.QueryAsync(db => db.Tags.AsNoTracking().SingleAsync(t => t.Id == tag.Id, Ct));

        Assert.Equal(ApplicationTestHost.DefaultNow.UtcDateTime, saved.CreatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, saved.CreatedAtUtc.Kind);
        Assert.Equal(saved.CreatedAtUtc, saved.UpdatedAtUtc);
    }

    [Fact]
    public async Task 型変換_日付と日時と絵文字を往復できる()
    {
        var owner = await _host.Data.CreateUserAsync();
        var project = await _host.Data.CreateProjectAsync(owner);
        var due = new DateOnly(2099, 12, 31);
        var task = await _host.Data.CreateTaskAsync(project, owner, "寿司🍣の手配", dueDate: due, category: StatusCategory.Done);

        var saved = await _host.Data.QueryAsync(db => db.TaskItems.AsNoTracking().SingleAsync(t => t.Id == task.Id, Ct));

        Assert.Equal("寿司🍣の手配", saved.Title);
        Assert.Equal(due, saved.DueDate);
        Assert.Equal(DateTimeKind.Utc, saved.CompletedAtUtc!.Value.Kind);
        Assert.Equal(task.CompletedAtUtc, saved.CompletedAtUtc);
        Assert.Equal(1, await _host.Data.QueryAsync(db => db.TaskItems.CountAsync(t => t.Id == task.Id && t.DueDate >= new DateOnly(2099, 1, 1), Ct)));
    }

    [Fact]
    public async Task 一意制約_同じプロジェクトキーは保存できない()
    {
        var owner = await _host.Data.CreateUserAsync();
        var project = await _host.Data.CreateProjectAsync(owner);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => _host.Data.QueryAsync(async db =>
        {
            db.Projects.Add(Project.Create(ProjectKey.Create(project.Key), Unique.Name("別名"), null));
            return await db.SaveChangesAsync(Ct);
        }));
        Assert.Equal(1062, FindMySqlException(ex).Number); // Duplicate entry
    }

    [Fact]
    public async Task CHECK制約_不正な値はDBでも拒否する()
    {
        var owner = await _host.Data.CreateUserAsync();
        var project = await _host.Data.CreateProjectAsync(owner);
        var task = await _host.Data.CreateTaskAsync(project, owner);

        await using var connection = new MySqlConnection(fixture.Database.ConnectionString);
        await connection.OpenAsync(Ct);
        foreach (var sql in new[]
                 {
                     "UPDATE task_item SET priority = 9 WHERE id = @id",
                     "UPDATE task_item SET start_date = '2026-10-10', due_date = '2026-10-01' WHERE id = @id",
                     "UPDATE task_item SET task_number = 0 WHERE id = @id",
                 })
        {
            await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // テスト内の固定 SQL
            command.CommandText = sql;
#pragma warning restore CA2100
            command.Parameters.AddWithValue("@id", task.Id.ToString());
            var ex = await Assert.ThrowsAsync<MySqlException>(() => command.ExecuteNonQueryAsync(Ct));
            Assert.Equal(3819, ex.Number); // Check constraint is violated
        }
    }

    [Fact]
    public async Task 外部キー_タスクが使っているステータスは直接削除できない()
    {
        var owner = await _host.Data.CreateUserAsync();
        var project = await _host.Data.CreateProjectAsync(owner);
        var task = await _host.Data.CreateTaskAsync(project, owner);

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => _host.Data.QueryAsync(db =>
            db.WorkflowStatuses.Where(s => s.Id == task.StatusId).ExecuteDeleteAsync(Ct)));
        Assert.Equal(1451, FindMySqlException(ex).Number); // Cannot delete or update a parent row
    }

    private static MySqlException FindMySqlException(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            if (current is MySqlException mysql)
            {
                return mysql;
            }
        }

        throw new Xunit.Sdk.XunitException($"MySqlException が含まれていません: {ex.GetType().Name}");
    }

    [Fact]
    public async Task 外部キー_親タスクを削除するとサブタスクも削除される()
    {
        var owner = await _host.Data.CreateUserAsync();
        var project = await _host.Data.CreateProjectAsync(owner);
        var parent = await _host.Data.CreateTaskAsync(project, owner, "親");
        var child = await _host.Data.CreateTaskAsync(project, owner, "子", parent: parent);

        await _host.Data.QueryAsync(async db =>
        {
            await db.TaskHistoryEntries.Where(h => h.TaskId == parent.Id || h.TaskId == child.Id).ExecuteDeleteAsync(Ct);
            return await db.TaskItems.Where(t => t.Id == parent.Id).ExecuteDeleteAsync(Ct);
        });

        Assert.False(await _host.Data.QueryAsync(db => db.TaskItems.AnyAsync(t => t.Id == child.Id, Ct)));
    }
}
