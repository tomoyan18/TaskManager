using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Abstractions;

namespace TaskManager.Infrastructure.Persistence;

/// <summary>
/// タスク番号の採番（database-design.md §5）。UPDATE が行ロックを取るため、同じプロジェクトへの同時作成は直列化される。
/// 呼び出し側のトランザクション内で実行すること。row_version は更新しない（設定の編集と競合させない）。
/// </summary>
public sealed class MySqlTaskNumberAllocator(AppDbContext db) : ITaskNumberAllocator
{
    public async Task<int?> AllocateAsync(Guid projectId, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("タスク番号の採番はトランザクション内で行ってください。");
        }

        var affected = await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE project SET last_task_number = last_task_number + 1 WHERE id = {projectId} AND archived_at_utc IS NULL",
            cancellationToken);
        if (affected == 0)
        {
            return null;
        }

        var number = await db.Projects.AsNoTracking().Where(p => p.Id == projectId).Select(p => p.LastTaskNumber)
            .SingleAsync(cancellationToken);
        return checked((int)number);
    }
}
