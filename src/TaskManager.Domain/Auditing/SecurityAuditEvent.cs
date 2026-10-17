using TaskManager.Domain.Common;

namespace TaskManager.Domain.Auditing;

/// <summary>セキュリティ監査記録（追記専用。domain-model.md §4.13）。秘密情報を含めない。</summary>
public sealed class SecurityAuditEvent
{
    public const int DetailMaxLength = 2000;
    public static readonly TimeSpan Retention = TimeSpan.FromDays(365);

    private SecurityAuditEvent()
    {
    }

    public Guid Id { get; private set; }

    public SecurityEventKind Kind { get; private set; }

    public Guid? ActorUserId { get; private set; }

    public Guid? SubjectUserId { get; private set; }

    public Guid? ProjectId { get; private set; }

    public string? IpAddress { get; private set; }

    public string? UserAgent { get; private set; }

    public string? DetailJson { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    public static SecurityAuditEvent Create(
        SecurityEventKind kind, Guid? actorUserId, Guid? subjectUserId, Guid? projectId,
        string? ipAddress, string? userAgent, string? detailJson, DateTime nowUtc) => new()
    {
        Id = Guid.CreateVersion7(),
        Kind = kind,
        ActorUserId = actorUserId,
        SubjectUserId = subjectUserId,
        ProjectId = projectId,
        IpAddress = ipAddress is { Length: > 45 } ? ipAddress[..45] : ipAddress,
        UserAgent = userAgent is { Length: > 255 } ? userAgent[..255] : userAgent,
        DetailJson = detailJson is { Length: > DetailMaxLength } ? null : detailJson,
        OccurredAtUtc = nowUtc,
    };
}
