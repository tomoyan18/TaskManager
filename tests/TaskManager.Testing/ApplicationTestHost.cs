using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using TaskManager.Application;
using TaskManager.Application.Abstractions;
using TaskManager.Infrastructure;

namespace TaskManager.Testing;

/// <summary>
/// Application 層のテスト用ホスト。本番と同じ DI 構成（Application + Infrastructure）で MySQL に接続し、
/// 現在の利用者・時刻・アクセストークン発行だけをテスト用に差し替える。
/// </summary>
public sealed class ApplicationTestHost : IAsyncDisposable
{
    /// <summary>既定の現在時刻: 2026-10-08（木）10:00 JST。</summary>
    public static readonly DateTimeOffset DefaultNow = new(2026, 10, 8, 1, 0, 0, TimeSpan.Zero);

    private readonly ServiceProvider _provider;

    public ApplicationTestHost(TestDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        AttachmentRoot = Path.Combine(Path.GetTempPath(), "tm-test-attachments", Guid.NewGuid().ToString("N"));
        Time = new FakeTimeProvider(DefaultNow);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{InfrastructureServiceCollectionExtensions.ConnectionStringName}"] = database.ConnectionString,
                ["Attachments:RootPath"] = AttachmentRoot,
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<TimeProvider>(Time);
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddScoped<TestCurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<TestCurrentUser>());
        services.AddSingleton<IAccessTokenIssuer, FakeAccessTokenIssuer>();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        Data = new TestDataBuilder(_provider);
    }

    public FakeTimeProvider Time { get; }

    public string AttachmentRoot { get; }

    public TestDataBuilder Data { get; }

    public IServiceProvider Services => _provider;

    /// <summary>actor としてサービスを呼ぶ（呼び出しごとに新しいスコープ = 1 リクエスト）。</summary>
    public async Task<TResult> RunAsync<TService, TResult>(TestUser? actor, Func<TService, Task<TResult>> action)
        where TService : notnull
    {
        ArgumentNullException.ThrowIfNull(action);
        await using var scope = _provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TestCurrentUser>().SignIn(actor);
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    public Task RunAsync<TService>(TestUser? actor, Func<TService, Task> action)
        where TService : notnull =>
        RunAsync<TService, bool>(actor, async s =>
        {
            await action(s);
            return true;
        });

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (Directory.Exists(AttachmentRoot))
        {
            Directory.Delete(AttachmentRoot, recursive: true);
        }
    }
}
