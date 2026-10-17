using TaskManager.Domain.Common;

namespace TaskManager.Domain.Tasks;

/// <summary>コメント（domain-model.md §4.9）。本文は Markdown。</summary>
public sealed class TaskComment
{
    public const int BodyMaxLength = 10000;

    private TaskComment()
    {
    }

    public Guid Id { get; private set; }

    public Guid TaskId { get; private set; }

    public Guid AuthorUserId { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? EditedAtUtc { get; private set; }

    public static TaskComment Create(Guid taskId, Guid authorUserId, string body, DateTime nowUtc) => new()
    {
        Id = Guid.CreateVersion7(),
        TaskId = taskId,
        AuthorUserId = authorUserId,
        Body = NormalizeBody(body),
        CreatedAtUtc = nowUtc,
    };

    public static string NormalizeBody(string? body) =>
        TextRules.OptionalMultiline(body, BodyMaxLength, "validation.failed", "body", "コメント")
        ?? throw new DomainException("validation.failed", "コメントを入力してください。", "body");

    public void Edit(string body, DateTime nowUtc)
    {
        Body = NormalizeBody(body);
        EditedAtUtc = nowUtc;
    }
}
