using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;
using TaskManager.Application.Abstractions;

namespace TaskManager.Infrastructure.Storage;

public sealed class AttachmentStorageOptions
{
    public const string SectionName = "Attachments";

    /// <summary>保存先のルート。Docker では /var/lib/taskmanager/attachments（ボリューム）。</summary>
    public string RootPath { get; set; } = "/var/lib/taskmanager/attachments";
}

/// <summary>
/// 添付ファイルをローカル（Docker ボリューム）に保存する（security-design.md S-11, S-12）。
/// 保存パスはサーバーが生成した「yyyy/MM/{Guid}」のみで、利用者の入力をパスに使わない。
/// </summary>
public sealed class LocalAttachmentStorage(IOptions<AttachmentStorageOptions> options, TimeProvider timeProvider) : IAttachmentStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.RootPath);

    public async Task<string> SaveAsync(Guid attachmentId, Stream content, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var key = string.Create(CultureInfo.InvariantCulture, $"{now:yyyy}/{now:MM}/{attachmentId:N}");
        var path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(file, cancellationToken);
        return key;
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = ResolvePath(storageKey);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = ResolvePath(storageKey);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<StoredFile> ListAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_root))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = Path.GetRelativePath(_root, path).Replace(Path.DirectorySeparatorChar, '/');
            yield return new StoredFile(key, File.GetLastWriteTimeUtc(path));
            await Task.Yield();
        }
    }

    /// <summary>キーを絶対パスに変換し、ルートの外を指していないことを確認する（パストラバーサル対策）。</summary>
    private string ResolvePath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey) || storageKey.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(storageKey))
        {
            throw new InvalidOperationException("不正な保存キーです。");
        }

        var full = Path.GetFullPath(Path.Combine(_root, storageKey));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("不正な保存キーです。");
        }

        return full;
    }
}
