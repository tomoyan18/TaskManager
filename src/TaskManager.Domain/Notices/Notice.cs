using TaskManager.Domain.Common;

namespace TaskManager.Domain.Notices;

/// <summary>お知らせ（domain-model.md §4.12）。本文は作成時点の日本語の文。</summary>
public sealed class Notice
{
    public const int MessageMaxLength = 300;
    public const int LinkPathMaxLength = 200;
    public static readonly TimeSpan MergeWindow = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan Retention = TimeSpan.FromDays(90);

    private Notice()
    {
    }

    public Guid Id { get; private set; }

    public Guid RecipientUserId { get; private set; }

    public NoticeKind Kind { get; private set; }

    public Guid? ActorUserId { get; private set; }

    public Guid? ProjectId { get; private set; }

    public Guid? TaskId { get; private set; }

    public string Message { get; private set; } = string.Empty;

    public string? LinkPath { get; private set; }

    public DateTime? ReadAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public static Notice Create(
        Guid recipientUserId, NoticeKind kind, Guid? actorUserId, Guid? projectId, Guid? taskId,
        string message, string? linkPath, DateTime nowUtc)
    {
        // ブラウザは /\ や /<タブ>/ で始まるパスを外部サイトとして扱うため、2 文字目の \ と制御文字・空白も拒否する（Phase 7）
        if (linkPath is not null && (!linkPath.StartsWith('/') || (linkPath.Length > 1 && linkPath[1] is '/' or '\\')
            || linkPath.Any(c => c == '\\' || char.IsWhiteSpace(c) || char.IsControl(c))))
        {
            throw new ArgumentException("linkPath は画面内の相対パス（/ で始まる）のみ指定できます。", nameof(linkPath));
        }

        return new Notice
        {
            Id = Guid.CreateVersion7(),
            RecipientUserId = recipientUserId,
            Kind = kind,
            ActorUserId = actorUserId,
            ProjectId = projectId,
            TaskId = taskId,
            Message = Truncate(message, MessageMaxLength),
            LinkPath = linkPath is null ? null : Truncate(linkPath, LinkPathMaxLength),
            CreatedAtUtc = nowUtc,
        };
    }

    public bool IsRead => ReadAtUtc is not null;

    public void MarkRead(DateTime nowUtc) => ReadAtUtc ??= nowUtc;

    /// <summary>N-02: 5 分以内の同種お知らせをまとめる（本文と日時を更新）。</summary>
    public void Refresh(Guid? actorUserId, string message, DateTime nowUtc)
    {
        ActorUserId = actorUserId;
        Message = Truncate(message, MessageMaxLength);
        CreatedAtUtc = nowUtc;
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";
}
