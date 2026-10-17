using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Common;
using TaskManager.Domain.Auditing;
using TaskManager.Domain.Notices;

namespace TaskManager.Application.Maintenance;

/// <summary>定期メンテナンス（architecture.md §5）。API プロセス内の BackgroundService から 1 時間ごとに呼ぶ。</summary>
public sealed partial class MaintenanceService(
    IAppDbContext db, IAttachmentStorage storage, TimeProvider timeProvider, ILogger<MaintenanceService> logger)
{
    public static readonly TimeSpan SessionRetention = TimeSpan.FromDays(30);
    public static readonly TimeSpan OrphanFileGracePeriod = TimeSpan.FromHours(1);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.UtcNow();
        var sessionCutoff = now - SessionRetention;
        var sessions = await db.RefreshSessions
            .Where(s => s.AbsoluteExpiresAtUtc < sessionCutoff || s.ExpiresAtUtc < sessionCutoff
                || (s.RevokedAtUtc != null && s.RevokedAtUtc < sessionCutoff) || (s.RotatedAtUtc != null && s.RotatedAtUtc < sessionCutoff))
            .ExecuteDeleteAsync(cancellationToken);
        var noticeCutoff = now - Notice.Retention;
        var notices = await db.Notices.Where(n => n.CreatedAtUtc < noticeCutoff).ExecuteDeleteAsync(cancellationToken);
        var auditCutoff = now - SecurityAuditEvent.Retention;
        var audits = await db.SecurityAuditEvents.Where(e => e.OccurredAtUtc < auditCutoff).ExecuteDeleteAsync(cancellationToken);

        var orphans = 0;
        await foreach (var file in storage.ListAsync(cancellationToken))
        {
            if (file.LastModifiedUtc > now - OrphanFileGracePeriod)
            {
                continue;
            }

            var key = file.StorageKey;
            if (!await db.TaskAttachments.AnyAsync(a => a.StorageKey == key, cancellationToken))
            {
                await storage.DeleteAsync(key, cancellationToken);
                orphans++;
            }
        }

        LogCompleted(logger, sessions, notices, audits, orphans);
    }

    [LoggerMessage(EventId = 9100, Level = LogLevel.Information,
        Message = "Maintenance completed: sessions={Sessions} notices={Notices} audits={Audits} orphanFiles={OrphanFiles}")]
    private static partial void LogCompleted(ILogger logger, int sessions, int notices, int audits, int orphanFiles);
}
