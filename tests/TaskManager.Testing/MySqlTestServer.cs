using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.Data.MySqlClient;
using TaskManager.Infrastructure.Persistence;
using Testcontainers.MySql;

namespace TaskManager.Testing;

/// <summary>
/// テストで使う MySQL サーバー（docs/testing/test-design.md §3）。
/// <list type="bullet">
/// <item>環境変数 <c>TM_TEST_MYSQL_CONNECTION</c> があれば、その MySQL（ローカル PC で起動したもの等）を使う。
/// DB の作成・削除ができる利用者の接続文字列を指定し、Database は指定しない。</item>
/// <item>なければ Testcontainers で MySQL コンテナ（既定 <c>mysql:8.4.11</c>、<c>TM_TEST_MYSQL_IMAGE</c> で変更可）を起動する。</item>
/// </list>
/// テスト用の DB は <c>tm_test_</c> で始まる名前で作成し、使い終わったら削除する。それ以外の DB には触れない。
/// </summary>
public sealed class MySqlTestServer
{
    public const string ConnectionVariable = "TM_TEST_MYSQL_CONNECTION";
    public const string ImageVariable = "TM_TEST_MYSQL_IMAGE";
    public const string DatabasePrefix = "tm_test_";
    private const string DefaultImage = "mysql:8.4.11";

    private static readonly SemaphoreSlim Gate = new(1, 1);

    // テスト用 DB には、EF Core のマイグレーションから生成した SQL を直接適用する。
    // db.Database.MigrateAsync() は MySQL のサーバー全体の名前付きロック（GET_LOCK('__EFMigrationsLock', -1)）を使い、
    // 並行するテストの中でまれに止まったまま進まなくなったため（Phase 7・8 で発生。security-review.md SEC-INFO-08）。
    // スクリプトは同じマイグレーションから作るため、適用される内容は MigrateAsync と同じ（__EFMigrationsHistory も記録される）。
    // マイグレーションの適用そのものは、Docker の migrate コンテナ（efbundle）と dotnet ef database update で確認している
    private static readonly Lazy<string> MigrationScript = new(() =>
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseMySQL("Server=localhost;Database=tm_script").Options);
        return db.GetService<IMigrator>().GenerateScript();
    });

    private static MySqlTestServer? _instance;

    private readonly string _adminConnectionString;

    private MySqlTestServer(string adminConnectionString, string source)
    {
        _adminConnectionString = adminConnectionString;
        Source = source;
    }

    /// <summary>接続先の説明（ログ・記録用。パスワードは含めない）。</summary>
    public string Source { get; }

    /// <summary>プロセス内で 1 つのサーバーを共有する。</summary>
    public static async Task<MySqlTestServer> GetAsync()
    {
        if (_instance is not null)
        {
            return _instance;
        }

        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _instance ??= await StartAsync().ConfigureAwait(false);
            return _instance;
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>空の DB を作成し、EF Core のマイグレーション（から生成した SQL）を適用する。</summary>
    public async Task<TestDatabase> CreateDatabaseAsync(CancellationToken cancellationToken = default)
    {
        var name = DatabasePrefix + Guid.NewGuid().ToString("N")[..12];
        await ExecuteAdminAsync(
            $"CREATE DATABASE `{name}` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci", cancellationToken).ConfigureAwait(false);
        var builder = new MySqlConnectionStringBuilder(_adminConnectionString) { Database = name };
        var database = new TestDatabase(this, name, builder.ConnectionString);
        await using (var connection = new MySqlConnection(builder.ConnectionString))
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await new MySqlScript(connection, MigrationScript.Value).ExecuteAsync(cancellationToken).ConfigureAwait(false);
        }

        return database;
    }

    internal async Task DropDatabaseAsync(string name)
    {
        // 自分で作成した名前以外は削除しない
        if (!name.StartsWith(DatabasePrefix, StringComparison.Ordinal) || name.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
        {
            throw new InvalidOperationException("テスト用以外の DB は削除できません。");
        }

        await ExecuteAdminAsync($"DROP DATABASE IF EXISTS `{name}`", CancellationToken.None).ConfigureAwait(false);
    }

    private async Task ExecuteAdminAsync(string sql, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_adminConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // DB 名は内部で生成した英数字のみ
        command.CommandText = sql;
#pragma warning restore CA2100
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<MySqlTestServer> StartAsync()
    {
        var configured = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var builder = new MySqlConnectionStringBuilder(configured);
            if (!string.IsNullOrEmpty(builder.Database))
            {
                throw new InvalidOperationException($"{ConnectionVariable} には Database を指定しないでください（テストごとに DB を作成します）。");
            }

            return new MySqlTestServer(configured, string.Create(
                CultureInfo.InvariantCulture, $"{ConnectionVariable}（{builder.Server}:{builder.Port}、利用者 {builder.UserID}）"));
        }

        var image = Environment.GetEnvironmentVariable(ImageVariable) is { Length: > 0 } custom ? custom : DefaultImage;
        var container = new MySqlBuilder(image)
            .WithUsername("root")
            .WithPassword(Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)))
            .Build();
        await container.StartAsync().ConfigureAwait(false);
        var admin = new MySqlConnectionStringBuilder(container.GetConnectionString()) { Database = string.Empty };
        return new MySqlTestServer(admin.ConnectionString, $"Testcontainers（{image}）");
    }
}

/// <summary>テスト用に作成した DB。破棄すると DB を削除する。</summary>
public sealed class TestDatabase : IAsyncDisposable
{
    private readonly MySqlTestServer _server;

    internal TestDatabase(MySqlTestServer server, string name, string connectionString)
    {
        _server = server;
        Name = name;
        ConnectionString = connectionString;
    }

    public string Name { get; }

    public string ConnectionString { get; }

    /// <summary>監査列の自動設定（インターセプター）なしのコンテキスト。検証・直接の確認用。</summary>
    public AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseMySQL(ConnectionString).Options);

    public async ValueTask DisposeAsync() => await _server.DropDatabaseAsync(Name).ConfigureAwait(false);
}
