using TaskManager.Domain.Common;

namespace TaskManager.Domain.Tasks;

/// <summary>タスクの変更履歴 1 件（domain-model.md §4.11）。値は表示用の文字列で保存する。</summary>
public sealed class TaskHistoryEntry
{
    public const int ValueMaxLength = 500;

    private TaskHistoryEntry()
    {
    }

    public Guid Id { get; private set; }

    public Guid TaskId { get; private set; }

    public TaskHistoryKind Kind { get; private set; }

    public Guid? ActorUserId { get; private set; }

    public string? OldValue { get; private set; }

    public string? NewValue { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    public static TaskHistoryEntry Create(
        Guid taskId, TaskHistoryKind kind, Guid? actorUserId, string? oldValue, string? newValue, DateTime nowUtc) => new()
    {
        Id = Guid.CreateVersion7(),
        TaskId = taskId,
        Kind = kind,
        ActorUserId = actorUserId,
        OldValue = Truncate(oldValue),
        NewValue = Truncate(newValue),
        OccurredAtUtc = nowUtc,
    };

    private static string? Truncate(string? value) =>
        value is null ? null : value.Length <= ValueMaxLength ? value : value[..(ValueMaxLength - 1)] + "…";
}
