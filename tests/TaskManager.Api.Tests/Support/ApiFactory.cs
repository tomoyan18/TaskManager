using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TaskManager.Application.Abstractions;
using TaskManager.Infrastructure.Persistence;
using TaskManager.Testing;

[assembly: AssemblyFixture(typeof(TaskManager.Api.Tests.Support.ApiFactory))]

namespace TaskManager.Api.Tests.Support;

/// <summary>
/// API テスト用のホスト（本番と同じ Program を TestServer で起動する）。
/// DB はテスト専用に作成した MySQL の DB、JWT の署名鍵はテストごとに生成した乱数を使う（Secret をリポジトリに置かない）。
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>テスト用ヘッダー: 送信元 IP を指定する（レート制限の区分をテストごとに分けるため）。</summary>
    public const string RemoteIpHeader = "X-Test-Remote-IP";

    public static readonly Uri BaseUri = new("https://localhost");

    private static int _ipCounter;
    private TestDatabase? _database;

    public string AttachmentRoot { get; } = Path.Combine(Path.GetTempPath(), "tm-api-test-attachments", Guid.NewGuid().ToString("N"));

    public string SigningKey { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    public TestDatabase Database => _database ?? throw new InvalidOperationException("DB が初期化されていません。");

    /// <summary>アプリが出力したログ（パスワード・トークンが含まれないことの確認に使う）。</summary>
    public ConcurrentQueue<string> Logs { get; } = new();

    public TestDataBuilder Data => new(Services);

    public async ValueTask InitializeAsync()
    {
        var server = await MySqlTestServer.GetAsync();
        _database = await server.CreateDatabaseAsync();
        _ = Server; // ホストを起動する
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (_database is not null)
        {
            await _database.DisposeAsync();
        }

        if (Directory.Exists(AttachmentRoot))
        {
            Directory.Delete(AttachmentRoot, recursive: true);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:TaskManager", Database.ConnectionString);
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("Swagger:Enabled", "true");
        builder.UseSetting("Attachments:RootPath", AttachmentRoot);
        builder.UseSetting("Maintenance:Enabled", "false");
        builder.UseSetting("AuthCookie:Secure", "true");
        builder.ConfigureTestServices(services =>
        {
            services.AddTransient<Microsoft.AspNetCore.Hosting.IStartupFilter, TestRemoteIpStartupFilter>();
            services.AddSingleton<Microsoft.Extensions.Logging.ILoggerProvider>(new CollectingLoggerProvider(Logs));
            // 500 応答の確認用: 特定のファイル名のときだけ例外を投げる（内部情報が応答に出ないことの確認）
            services.Replace(ServiceDescriptor.Singleton<IAttachmentContentInspector>(sp =>
                new FaultInjectingInspector(new Infrastructure.Storage.AttachmentContentInspector())));
        });
    }

    /// <summary>未ログインのクライアント（Cookie を保持し、送信元 IP はクライアントごとに異なる）。</summary>
    public HttpClient CreateAnonymousClient(string? remoteIp = null)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = BaseUri,
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
        client.DefaultRequestHeaders.Add(RemoteIpHeader, remoteIp ?? NextIp());
        return client;
    }

    /// <summary>利用者としてアクセストークンを付けたクライアント（ログイン API を経由せず、本番と同じ発行処理でトークンを作る）。</summary>
    public async Task<HttpClient> CreateClientAsync(TestUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var client = CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await IssueTokenAsync(user));
        return client;
    }

    public async Task<string> IssueTokenAsync(TestUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var account = await db.UserAccounts.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        return scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>().Issue(account, Guid.NewGuid()).Value;
    }

    private static string NextIp()
    {
        var n = Interlocked.Increment(ref _ipCounter);
        return $"10.{(n >> 16) & 255}.{(n >> 8) & 255}.{n & 255}";
    }

    private sealed class TestRemoteIpStartupFilter : Microsoft.AspNetCore.Hosting.IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((HttpContext context, RequestDelegate nextMiddleware) =>
            {
                if (context.Request.Headers.TryGetValue(RemoteIpHeader, out var value) && IPAddress.TryParse(value.ToString(), out var ip))
                {
                    context.Connection.RemoteIpAddress = ip;
                }

                return nextMiddleware(context);
            });
            next(app);
        };
    }

    private sealed class FaultInjectingInspector(IAttachmentContentInspector inner) : IAttachmentContentInspector
    {
        public const string TriggerFileName = "trigger-internal-error.txt";

        public AttachmentInspection Inspect(string fileName, ReadOnlySpan<byte> header) =>
            fileName == TriggerFileName
                ? throw new InvalidOperationException("SECRET-INTERNAL-DETAIL Server=db;Password=should-not-leak")
                : inner.Inspect(fileName, header);
    }
}

/// <summary>ログを集める（ログの内容の検査用）。</summary>
public sealed class CollectingLoggerProvider(ConcurrentQueue<string> sink) : Microsoft.Extensions.Logging.ILoggerProvider
{
    public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) => new Logger(categoryName, sink);

    public void Dispose()
    {
    }

    private sealed class Logger(string category, ConcurrentQueue<string> sink) : Microsoft.Extensions.Logging.ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            sink.Enqueue($"{logLevel} {category}: {formatter(state, exception)} {exception}");
        }
    }
}
