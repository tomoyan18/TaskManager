using TaskManager.Domain.Accounts;

namespace TaskManager.Application.Abstractions;

/// <summary>現在の利用者（Api が JWT のクレームから実装する）。</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>ログイン中の利用者 ID。未ログインで参照すると例外。</summary>
    Guid UserId { get; }

    bool IsSystemAdministrator { get; }

    /// <summary>リフレッシュセッションの系列 ID（アクセストークンの sid）。</summary>
    Guid? SessionFamilyId { get; }

    string? IpAddress { get; }

    string? UserAgent { get; }
}

public enum PasswordCheckResult
{
    Failed,
    Success,
    SuccessRehashNeeded,
}

public interface IPasswordHasher
{
    string Hash(string password);

    PasswordCheckResult Verify(string passwordHash, string password);
}

public sealed record AccessToken(string Value, int ExpiresInSeconds);

public interface IAccessTokenIssuer
{
    AccessToken Issue(UserAccount user, Guid sessionFamilyId);
}

public sealed record GeneratedRefreshToken(string Token, byte[] Hash);

public interface IRefreshTokenGenerator
{
    GeneratedRefreshToken Generate();

    byte[] Hash(string token);
}

public interface ITemporaryPasswordGenerator
{
    string Generate();
}

/// <summary>タスク番号の採番（プロジェクト単位で直列化。database-design.md §5）。トランザクション内で呼ぶこと。</summary>
public interface ITaskNumberAllocator
{
    /// <summary>次の番号を返す。プロジェクトが存在しないかアーカイブ中なら null。</summary>
    Task<int?> AllocateAsync(Guid projectId, CancellationToken cancellationToken);
}

public interface IAttachmentStorage
{
    /// <summary>ファイルを保存し、保存先のキー（yyyy/MM/{id}）を返す。</summary>
    Task<string> SaveAsync(Guid attachmentId, Stream content, CancellationToken cancellationToken);

    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);

    /// <summary>保存されているすべてのファイル（孤立ファイルの削除に使う）。</summary>
    IAsyncEnumerable<StoredFile> ListAsync(CancellationToken cancellationToken);
}

public sealed record StoredFile(string StorageKey, DateTime LastModifiedUtc);

public sealed record AttachmentInspection(bool IsAllowed, string? ContentType);

public interface IAttachmentContentInspector
{
    /// <summary>拡張子と先頭バイトから、許可された種類かと保存する Content-Type を判定する。</summary>
    AttachmentInspection Inspect(string fileName, ReadOnlySpan<byte> header);
}
