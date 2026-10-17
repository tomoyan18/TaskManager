using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Common;
using TaskManager.Application.Notices;
using TaskManager.Application.Security;
using TaskManager.Domain.Common;
using TaskManager.Domain.Tasks;

namespace TaskManager.Application.Tasks;

/// <summary>コメント（REQ-CMT-01）。古い順。</summary>
public sealed class TaskCommentService(
    IAppDbContext db, ICurrentUser currentUser, ProjectAccessGuard guard, NoticeWriter noticeWriter, TimeProvider timeProvider)
{
    public async Task<PagedResponse<TaskCommentResponse>> ListAsync(
        string projectKey, int taskNumber, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.ViewProject, cancellationToken);
        var task = await LoadTaskAsync(access.ProjectId, taskNumber, cancellationToken);
        var paging = PageRequest.Of(page, pageSize, 50);
        var query = db.TaskComments.AsNoTracking().Where(c => c.TaskId == task.Id);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(c => c.CreatedAtUtc).ThenBy(c => c.Id)
            .Skip(paging.Skip).Take(paging.PageSize).ToListAsync(cancellationToken);
        var authorIds = rows.Select(r => r.AuthorUserId).Distinct().ToList();
        var authors = await db.UserAccounts.AsNoTracking().Where(u => authorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new UserRef(u.Id, u.DisplayName, u.IsActive), cancellationToken);
        return PagedResponse<TaskCommentResponse>.Create(rows.Select(c => ToResponse(c, authors[c.AuthorUserId], access)).ToList(), paging, total);
    }

    public async Task<TaskCommentResponse> CreateAsync(
        string projectKey, int taskNumber, CreateTaskCommentRequest request, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.Comment, cancellationToken);
        var task = await LoadTaskAsync(access.ProjectId, taskNumber, cancellationToken);
        var now = timeProvider.UtcNow();
        TaskComment comment;
        try
        {
            comment = TaskComment.Create(task.Id, currentUser.UserId, request.Body, now);
        }
        catch (DomainException ex)
        {
            throw Errors.FromDomain(ex);
        }

        db.TaskComments.Add(comment);
        var author = await db.UserAccounts.AsNoTracking().SingleAsync(u => u.Id == currentUser.UserId, cancellationToken);
        var recipients = new[] { task.AssigneeUserId, task.CreatedByUserId }
            .Where(id => id is not null && id != currentUser.UserId).Select(id => id!.Value).Distinct();
        foreach (var recipient in recipients)
        {
            await noticeWriter.NotifyAsync(recipient, NoticeKind.TaskCommented, currentUser.UserId, access.ProjectId, task.Id,
                $"{author.DisplayName}さんが {NoticeWriter.TaskLabel(access.ProjectKey, task.Number, task.Title)} にコメントしました",
                NoticeWriter.TaskLink(access.ProjectKey, task.Number), cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(comment, new UserRef(author.Id, author.DisplayName, author.IsActive), access);
    }

    public async Task<TaskCommentResponse> UpdateAsync(
        string projectKey, int taskNumber, Guid commentId, UpdateTaskCommentRequest request, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.Comment, cancellationToken);
        var task = await LoadTaskAsync(access.ProjectId, taskNumber, cancellationToken);
        var comment = await db.TaskComments.SingleOrDefaultAsync(c => c.TaskId == task.Id && c.Id == commentId, cancellationToken)
            ?? throw Errors.NotFound();
        if (comment.AuthorUserId != currentUser.UserId)
        {
            throw Errors.Forbidden();
        }

        try
        {
            comment.Edit(request.Body, timeProvider.UtcNow());
        }
        catch (DomainException ex)
        {
            throw Errors.FromDomain(ex);
        }

        await db.SaveChangesAsync(cancellationToken);
        var author = await db.UserAccounts.AsNoTracking().SingleAsync(u => u.Id == comment.AuthorUserId, cancellationToken);
        return ToResponse(comment, new UserRef(author.Id, author.DisplayName, author.IsActive), access);
    }

    public async Task DeleteAsync(string projectKey, int taskNumber, Guid commentId, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.Comment, cancellationToken);
        var task = await LoadTaskAsync(access.ProjectId, taskNumber, cancellationToken);
        var comment = await db.TaskComments.SingleOrDefaultAsync(c => c.TaskId == task.Id && c.Id == commentId, cancellationToken)
            ?? throw Errors.NotFound();
        if (comment.AuthorUserId != currentUser.UserId && !access.Has(ProjectPermission.DeleteAnyComment))
        {
            throw Errors.Forbidden();
        }

        db.TaskComments.Remove(comment);
        await db.SaveChangesAsync(cancellationToken);
    }

    private TaskCommentResponse ToResponse(TaskComment c, UserRef author, ProjectAccessContext access)
    {
        var writable = !access.IsArchived && access.Has(ProjectPermission.Comment);
        var isAuthor = c.AuthorUserId == currentUser.UserId;
        return new TaskCommentResponse(c.Id, author, c.Body, c.CreatedAtUtc, c.EditedAtUtc,
            writable && isAuthor, writable && (isAuthor || access.Has(ProjectPermission.DeleteAnyComment)));
    }

    private async Task<TaskItem> LoadTaskAsync(Guid projectId, int taskNumber, CancellationToken cancellationToken) =>
        await db.TaskItems.AsNoTracking().SingleOrDefaultAsync(t => t.ProjectId == projectId && t.Number == taskNumber, cancellationToken)
        ?? throw Errors.NotFound();
}
