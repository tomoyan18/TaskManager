using TaskManager.Testing;

[assembly: AssemblyFixture(typeof(DatabaseFixture))]

namespace TaskManager.Application.Tests;

/// <summary>
/// Application 層のテストの基底。アセンブリで共有する MySQL の DB に、本番と同じ DI 構成で接続する。
/// テストごとに一意なデータを作るため、他のテストのデータとは干渉しない。
/// </summary>
public abstract class ApplicationTestBase(DatabaseFixture fixture) : IAsyncLifetime
{
    protected ApplicationTestHost Host { get; private set; } = null!;

    protected TestDataBuilder Data => Host.Data;

    protected DatabaseFixture Fixture => fixture;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public virtual ValueTask InitializeAsync()
    {
        Host = new ApplicationTestHost(fixture.Database);
        return ValueTask.CompletedTask;
    }

    public virtual async ValueTask DisposeAsync()
    {
        await Host.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
