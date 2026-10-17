using Microsoft.Extensions.Diagnostics.HealthChecks;
using TaskManager.Infrastructure.Persistence;

namespace TaskManager.Api.Health;

/// <summary>/health/ready 用。MySQL に接続できるかを確認する。</summary>
public sealed class DatabaseHealthCheck(AppDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await dbContext.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("データベースに接続できません。");
        }
#pragma warning disable CA1031 // ヘルスチェックはあらゆる失敗を Unhealthy として返す
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return HealthCheckResult.Unhealthy("データベースに接続できません。", ex);
        }
    }
}
