using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Users;
using TaskManager.Infrastructure.Persistence;

namespace TaskManager.Api.Background;

/// <summary>起動時の処理（migration-strategy.md §2）: スキーマが最新かの確認と、初期システム管理者の作成。</summary>
public static partial class StartupTasks
{
    public static async Task RunAsync(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var configuration = app.Configuration;
        if (!configuration.GetValue("Database:RunStartupTasks", true))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("TaskManager.Startup");
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count > 0)
        {
            var names = string.Join(", ", pending);
            LogPendingMigrations(logger, names);
            throw new InvalidOperationException("データベースが最新ではありません。migrate を実行してください。");
        }

        await scope.ServiceProvider.GetRequiredService<BootstrapAdministratorService>().EnsureAsync(
            configuration["Bootstrap:AdminEmail"], configuration["Bootstrap:AdminPassword"], CancellationToken.None);
    }

    [LoggerMessage(EventId = 9300, Level = LogLevel.Critical,
        Message = "データベースが最新ではありません。migrate を実行してください。未適用: {Migrations}")]
    private static partial void LogPendingMigrations(ILogger logger, string migrations);
}
