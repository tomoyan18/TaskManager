using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Common;
using TaskManager.Application.Security;
using TaskManager.Domain.Common;
using TaskManager.Domain.Tasks;

namespace TaskManager.Application.Tasks;

/// <summary>添付ファイル（REQ-ATT-01、security-design.md S-11, S-12）。</summary>
public sealed class TaskAttachmentService(
    IAppDbContext db,
    ICurrentUser currentUser,
    ProjectAccessGuard guard,
    IAttachmentStorage storage,
    IAttachmentContentInspector inspector,
    TimeProvider timeProvider)
{
    private const int HeaderLength = 512;

    public async Task<IReadOnlyList<TaskAttachmentResponse>> ListAsync(string projectKey, int taskNumber, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.ViewProject, cancellationToken);
        var task = await LoadTaskAsync(access.ProjectId, taskNumber, cancellationToken);
        var rows = await db.TaskAttachments.AsNoTracking().Where(a => a.TaskId == task.Id)
            .OrderByDescending(a => a.UploadedAtUtc).ToListAsync(cancellationToken);
        var userIds = rows.Select(r => r.UploadedByUserId).Distinct().ToList();
        var users = await db.UserAccounts.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new UserRef(u.Id, u.DisplayName, u.IsActive), cancellationToken);
        return rows.Select(a => ToResponse(a, users[a.UploadedByUserId], access)).ToList();
    }

    /// <summary>content は先頭に戻せるストリーム（ASP.NET Core の IFormFile は対応）であること。</summary>
    public async Task<TaskAttachmentResponse> UploadAsync(
        string projectKey, int taskNumber, string fileName, Stream content, long length, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        var access = await guard.RequireAsync(projectKey, ProjectPermission.UploadAttachments, cancellationToken);
        var task = await LoadTaskAsync(access.ProjectId, taskNumber, cancellationToken);

        if (length > TaskAttachment.MaxSizeBytes)
        {
            throw new AppException(AppErrorKind.PayloadTooLarge, ErrorCodes.AttachmentTooLarge, "ファイルサイズが上限（10MB）を超えています。");
        }

        if (length <= 0)
        {
            throw Errors.Validation("file", "空のファイルは添付できません。");
        }

        if (await db.TaskAttachments.CountAsync(a => a.TaskId == task.Id, cancellationToken) >= TaskAttachment.MaxPerTask)
        {
            throw Errors.Conflict(ErrorCodes.AttachmentLimitReached, "1 つのタスクに添付できるファイルは 20 個までです。");
        }

        if (!content.CanSeek)
        {
            throw new InvalidOperationException("添付ファイルのストリームは Seek 可能である必要があります。");
        }

        var header = new byte[HeaderLength];
        content.Position = 0;
        var read = await content.ReadAtLeastAsync(header, HeaderLength, throwOnEndOfStream: false, cancellationToken);
        var normalizedName = TaskAttachment.NormalizeFileName(fileName);
        var inspection = inspector.Inspect(normalizedName, header.AsSpan(0, read));
        if (!inspection.IsAllowed || inspection.ContentType is null)
        {
            throw new AppException(AppErrorKind.UnsupportedMediaType, ErrorCodes.AttachmentTypeNotAllowed,
                "この種類のファイルは添付できません。",
                "添付できるのは PDF、画像（PNG・JPEG・GIF・WebP）、テキスト（TXT・CSV・MD）、Word・Excel・PowerPoint、ZIP です。");
        }

        content.Position = 0;
        var sha256 = await SHA256.HashDataAsync(content, cancellationToken);
        content.Position = 0;
        var id = Guid.CreateVersion7();
        var storageKey = await storage.SaveAsync(id, content, cancellationToken);
        var now = timeProvider.UtcNow();
        var attachment = TaskAttachment.Create(id, task.Id, normalizedName, inspection.ContentType, length, storageKey, sha256, currentUser.UserId, now);
        db.TaskAttachments.Add(attachment);
        db.TaskHistoryEntries.Add(TaskHistoryEntry.Create(task.Id, TaskHistoryKind.AttachmentAdded, currentUser.UserId, null, attachment.OriginalFileName, now));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await storage.DeleteAsync(storageKey, CancellationToken.None);
            throw;
        }

        var user = await db.UserAccounts.AsNoTracking().SingleAsync(u => u.Id == currentUser.UserId, cancellationToken);
        return ToResponse(attachment, new UserRef(user.Id, user.DisplayName, user.IsActive), access);
    }

    public async Task<AttachmentContent> OpenReadAsync(string projectKey, int taskNumber, Guid attachmentId, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.ViewProject, cancellationToken);
        var task = await LoadTaskAsync(access.ProjectId, taskNumber, cancellationToken);
        var attachment = await db.TaskAttachments.AsNoTracking()
            .SingleOrDefaultAsync(a => a.TaskId == task.Id && a.Id == attachmentId, cancellationToken) ?? throw Errors.NotFound();
        var stream = await storage.OpenReadAsync(attachment.StorageKey, cancellationToken) ?? throw Errors.NotFound();
        return new AttachmentContent(stream, attachment.OriginalFileName, attachment.ContentType, attachment.SizeBytes);
    }

    public async Task DeleteAsync(string projectKey, int taskNumber, Guid attachmentId, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.UploadAttachments, cancellationToken);
        var task = await LoadTaskAsync(access.ProjectId, taskNumber, cancellationToken);
        var attachment = await db.TaskAttachments
            .SingleOrDefaultAsync(a => a.TaskId == task.Id && a.Id == attachmentId, cancellationToken) ?? throw Errors.NotFound();
        if (attachment.UploadedByUserId != currentUser.UserId && !access.Has(ProjectPermission.DeleteAnyAttachment))
        {
            throw Errors.Forbidden();
        }

        db.TaskAttachments.Remove(attachment);
        db.TaskHistoryEntries.Add(TaskHistoryEntry.Create(
            task.Id, TaskHistoryKind.AttachmentRemoved, currentUser.UserId, attachment.OriginalFileName, null, timeProvider.UtcNow()));
        await db.SaveChangesAsync(cancellationToken);
        await storage.DeleteAsync(attachment.StorageKey, cancellationToken);
    }

    private TaskAttachmentResponse ToResponse(TaskAttachment a, UserRef uploadedBy, ProjectAccessContext access)
    {
        var writable = !access.IsArchived && access.Has(ProjectPermission.UploadAttachments);
        var canDelete = writable && (a.UploadedByUserId == currentUser.UserId || access.Has(ProjectPermission.DeleteAnyAttachment));
        return new TaskAttachmentResponse(a.Id, a.OriginalFileName, a.ContentType, a.SizeBytes, uploadedBy, a.UploadedAtUtc, canDelete);
    }

    private async Task<TaskItem> LoadTaskAsync(Guid projectId, int taskNumber, CancellationToken cancellationToken) =>
        await db.TaskItems.AsNoTracking().SingleOrDefaultAsync(t => t.ProjectId == projectId && t.Number == taskNumber, cancellationToken)
        ?? throw Errors.NotFound();
}
