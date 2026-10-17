using TaskManager.Application.Maintenance;

namespace TaskManager.Api.Background;

/// <summary>1 時間ごとの定期メンテナンス（architecture.md §5）。Maintenance:Enabled=false で無効化できる。</summary>
public sealed partial class MaintenanceWorker(IServiceScopeFactory scopeFactory, ILogger<MaintenanceWorker> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<MaintenanceService>().RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // 定期処理の失敗でアプリ全体を止めない（次回に再実行する）
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(EventId = 9101, Level = LogLevel.Error, Message = "定期メンテナンスに失敗しました。次回に再実行します。")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
