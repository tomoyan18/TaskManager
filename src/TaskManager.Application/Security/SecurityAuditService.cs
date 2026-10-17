using System.Text.Json;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Common;
using TaskManager.Domain.Auditing;
using TaskManager.Domain.Common;
using Microsoft.Extensions.Logging;

namespace TaskManager.Application.Security;

/// <summary>
/// セキュリティ監査記録（REQ-AUD-01）。DB に追加し（保存は呼び出し側の SaveChanges で行う）、構造化ログにも出す。
/// パスワード・トークンは記録しない。
/// </summary>
public sealed partial class SecurityAuditService(
    IAppDbContext db, ICurrentUser currentUser, TimeProvider timeProvider, ILogger<SecurityAuditService> logger)
{
    public void Record(
        SecurityEventKind kind, Guid? subjectUserId = null, Guid? projectId = null,
        IReadOnlyDictionary<string, string?>? detail = null, Guid? actorUserId = null)
    {
        var actor = actorUserId ?? (currentUser.IsAuthenticated ? currentUser.UserId : null);
        var detailJson = detail is null ? null : JsonSerializer.Serialize(detail);
        db.SecurityAuditEvents.Add(SecurityAuditEvent.Create(
            kind, actor, subjectUserId, projectId, currentUser.IpAddress, currentUser.UserAgent, detailJson,
            timeProvider.UtcNow()));
        LogSecurityEvent(logger, kind, actor, subjectUserId, projectId, currentUser.IpAddress);
    }

    [LoggerMessage(EventId = 9000, Level = LogLevel.Information,
        Message = "SecurityEvent {Kind} actor={ActorUserId} subject={SubjectUserId} project={ProjectId} ip={IpAddress}")]
    private static partial void LogSecurityEvent(
        ILogger logger, SecurityEventKind kind, Guid? actorUserId, Guid? subjectUserId, Guid? projectId, string? ipAddress);
}
