using System.Text;

namespace TaskManager.Domain.Tasks;

/// <summary>添付ファイルのメタ情報（domain-model.md §4.10）。実体は保存先に StorageKey で置く。</summary>
public sealed class TaskAttachment
{
    public const long MaxSizeBytes = 10 * 1024 * 1024;
    public const int MaxPerTask = 20;
    public const int FileNameMaxLength = 255;

    private TaskAttachment()
    {
    }

    public Guid Id { get; private set; }

    public Guid TaskId { get; private set; }

    public string OriginalFileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public string StorageKey { get; private set; } = string.Empty;

#pragma warning disable CA1819 // EF Core の BINARY(32) 列に対応させるため配列を公開する
    public byte[] Sha256 { get; private set; } = [];
#pragma warning restore CA1819

    public Guid UploadedByUserId { get; private set; }

    public DateTime UploadedAtUtc { get; private set; }

    public static TaskAttachment Create(
        Guid id, Guid taskId, string originalFileName, string contentType, long sizeBytes, string storageKey,
        byte[] sha256, Guid uploadedByUserId, DateTime nowUtc) => new()
    {
        Id = id,
        TaskId = taskId,
        OriginalFileName = NormalizeFileName(originalFileName),
        ContentType = contentType,
        SizeBytes = sizeBytes,
        StorageKey = storageKey,
        Sha256 = sha256,
        UploadedByUserId = uploadedByUserId,
        UploadedAtUtc = nowUtc,
    };

    /// <summary>表示用のファイル名に正規化する（パス区切り・制御文字を除去し、255 文字に収める）。</summary>
    public static string NormalizeFileName(string? fileName)
    {
        var name = (fileName ?? string.Empty).Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];
        var builder = new StringBuilder(name.Length);
        foreach (var c in name.Where(c => !char.IsControl(c) && c is not ':' and not '*' and not '?' and not '"' and not '<' and not '>' and not '|'))
        {
            builder.Append(c);
        }

        var normalized = builder.ToString().Trim().TrimStart('.');
        if (normalized.Length == 0)
        {
            normalized = "file";
        }

        if (normalized.Length > FileNameMaxLength)
        {
            var extension = Path.GetExtension(normalized);
            normalized = extension.Length < 20
                ? string.Concat(normalized.AsSpan(0, FileNameMaxLength - extension.Length), extension)
                : normalized[..FileNameMaxLength];
        }

        return normalized;
    }
}
