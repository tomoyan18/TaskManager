namespace TaskManager.Testing;

/// <summary>
/// テストアセンブリで共有する DB（xUnit の AssemblyFixture として使う）。
/// テストは一意なキー・メールアドレスでデータを作るため、同じ DB を並行して使ってよい。
/// DB 全体の状態に依存するテスト（利用者一覧の件数、初期管理者の作成など）は <see cref="CreateIsolatedDatabaseAsync"/> を使う。
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private TestDatabase? _database;

    public TestDatabase Database => _database ?? throw new InvalidOperationException("DB が初期化されていません。");

    public MySqlTestServer Server { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        Server = await MySqlTestServer.GetAsync();
        _database = await Server.CreateDatabaseAsync();
    }

    public Task<TestDatabase> CreateIsolatedDatabaseAsync() => Server.CreateDatabaseAsync();

    public async ValueTask DisposeAsync()
    {
        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }
}
