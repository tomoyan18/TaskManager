using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Common;
using TaskManager.Application.Security;
using TaskManager.Application.Users;
using TaskManager.Domain.Common;
using TaskManager.Domain.Projects;

namespace TaskManager.Application.Projects;

/// <summary>プロジェクト（REQ-PRJ-01〜05）。</summary>
public sealed class ProjectService(
    IAppDbContext db,
    ICurrentUser currentUser,
    ProjectAccessGuard guard,
    IAttachmentStorage attachmentStorage,
    SecurityAuditService audit,
    TimeProvider timeProvider)
{
    public async Task<PagedResponse<ProjectSummaryResponse>> ListAsync(
        bool includeArchived, string? q, string? sort, bool allScope, PageRequest page, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        var all = allScope && currentUser.IsSystemAdministrator;
        var query = db.Projects.AsNoTracking();
        if (!all)
        {
            query = query.Where(p => db.ProjectMemberships.Any(m => m.ProjectId == p.Id && m.UserId == userId));
        }

        if (!includeArchived)
        {
            query = query.Where(p => p.ArchivedAtUtc == null);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(p => p.Name.Contains(term) || p.Key.Contains(term));
        }

        query = (sort ?? "name") switch
        {
            "name" => query.OrderBy(p => p.Name),
            "key" => query.OrderBy(p => p.Key),
            "-updatedAt" => query.OrderByDescending(p => p.UpdatedAtUtc),
            _ => throw UserAdministrationService.InvalidSort(),
        };

        var total = await query.CountAsync(cancellationToken);
        var projects = await query.Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken);
        var ids = projects.Select(p => p.Id).ToList();
        var today = timeProvider.TodayIn(await UserTimeZoneAsync(cancellationToken));

        var roles = await db.ProjectMemberships.AsNoTracking()
            .Where(m => ids.Contains(m.ProjectId) && m.UserId == userId)
            .ToDictionaryAsync(m => m.ProjectId, m => m.Role, cancellationToken);
        var owners = (await db.ProjectMemberships.AsNoTracking()
                .Where(m => ids.Contains(m.ProjectId) && m.Role == ProjectRole.Owner)
                .Join(db.UserAccounts, m => m.UserId, u => u.Id, (m, u) => new { m.ProjectId, u.DisplayName })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.ProjectId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.DisplayName).OrderBy(n => n).Take(3).ToList());

        var counts = (await db.TaskItems.AsNoTracking()
                .Where(t => ids.Contains(t.ProjectId))
                .Join(db.WorkflowStatuses, t => t.StatusId, s => s.Id, (t, s) => new { t.ProjectId, s.Category, t.DueDate })
                .Where(x => x.Category == StatusCategory.NotStarted || x.Category == StatusCategory.InProgress)
                .GroupBy(x => x.ProjectId)
                .Select(g => new { ProjectId = g.Key, Open = g.Count(), Overdue = g.Count(x => x.DueDate != null && x.DueDate < today) })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.ProjectId);

        var items = projects.Select(p =>
        {
            // システム管理者が「すべて」を見る場合、参加していないプロジェクトの内容（件数）は返さない
            var role = roles.TryGetValue(p.Id, out var r) ? r : (ProjectRole?)null;
            var c = role is not null && counts.TryGetValue(p.Id, out var cc) ? cc : null;
            return new ProjectSummaryResponse(
                p.Key, p.Name, role, p.IsArchived, c?.Open ?? 0, c?.Overdue ?? 0,
                owners.TryGetValue(p.Id, out var o) ? o : [], p.UpdatedAtUtc);
        }).ToList();
        return PagedResponse<ProjectSummaryResponse>.Create(items, page, total);
    }

    public async Task<ProjectDetailResponse> GetAsync(string projectKey, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.ViewProject, cancellationToken);
        return await BuildDetailAsync(access.ProjectId, access.Role, cancellationToken);
    }

    public async Task<ProjectDetailResponse> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        ProjectKey key;
        Project project;
        try
        {
            key = ProjectKey.Create(request.Key);
            project = Project.Create(key, request.Name, request.Description);
        }
        catch (DomainException ex)
        {
            throw Errors.FromDomain(ex);
        }

        if (await db.Projects.AnyAsync(p => p.Key == key.Value, cancellationToken))
        {
            throw Errors.Conflict(ErrorCodes.ProjectKeyTaken, "このプロジェクトキーは既に使われています。");
        }

        if (await db.Projects.AnyAsync(p => p.Name == project.Name, cancellationToken))
        {
            throw Errors.Conflict(ErrorCodes.ProjectNameTaken, "同じ名前のプロジェクトが既にあります。");
        }

        var now = timeProvider.UtcNow();
        db.Projects.Add(project);
        db.WorkflowStatuses.AddRange(project.CreateDefaultStatuses());
        db.ProjectMemberships.Add(ProjectMembership.Create(project.Id, currentUser.UserId, ProjectRole.Owner, currentUser.UserId, now));
        await db.SaveChangesAsync(cancellationToken);
        return await BuildDetailAsync(project.Id, ProjectRole.Owner, cancellationToken);
    }

    public async Task<ProjectDetailResponse> UpdateAsync(string projectKey, UpdateProjectRequest request, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.EditProjectSettings, cancellationToken);
        var project = await db.Projects.SingleAsync(p => p.Id == access.ProjectId, cancellationToken);
        db.SetExpectedVersion(project, request.Version);
        if (project.RowVersion != request.Version)
        {
            throw Errors.ConcurrencyConflict();
        }

        try
        {
            if (request.IsSpecified(nameof(UpdateProjectRequest.Name)))
            {
                var name = Project.NormalizeName(request.Name);
                if (name != project.Name && await db.Projects.AnyAsync(p => p.Id != project.Id && p.Name == name, cancellationToken))
                {
                    throw Errors.Conflict(ErrorCodes.ProjectNameTaken, "同じ名前のプロジェクトが既にあります。");
                }

                project.Rename(name);
            }

            if (request.IsSpecified(nameof(UpdateProjectRequest.Description)))
            {
                project.ChangeDescription(request.Description);
            }
        }
        catch (DomainException ex)
        {
            throw Errors.FromDomain(ex);
        }

        await db.SaveChangesAsync(cancellationToken);
        return await BuildDetailAsync(project.Id, access.Role, cancellationToken);
    }

    public async Task<ProjectDetailResponse> ArchiveAsync(string projectKey, bool archive, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.ArchiveProject, cancellationToken);
        var project = await db.Projects.SingleAsync(p => p.Id == access.ProjectId, cancellationToken);
        if (archive)
        {
            project.Archive(timeProvider.UtcNow());
        }
        else
        {
            project.Unarchive();
        }

        await db.SaveChangesAsync(cancellationToken);
        return await BuildDetailAsync(project.Id, access.Role, cancellationToken);
    }

    /// <summary>アーカイブ済みプロジェクトの削除（SA のみ。database-design.md §4 の順序で削除）。</summary>
    public async Task DeleteAsync(string projectKey, string? confirmKey, CancellationToken cancellationToken)
    {
        if (!currentUser.IsSystemAdministrator)
        {
            throw Errors.Forbidden();
        }

        var access = await guard.RequireAsync(projectKey, ProjectPermission.DeleteProject, cancellationToken);
        if (!string.Equals(confirmKey?.Trim(), access.ProjectKey, StringComparison.OrdinalIgnoreCase))
        {
            throw Errors.Validation("confirmKey", "確認のため、プロジェクトキーを正しく入力してください。");
        }

        if (!access.IsArchived)
        {
            throw Errors.Conflict(ErrorCodes.ProjectNotArchived, "削除する前にプロジェクトをアーカイブしてください。");
        }

        var projectId = access.ProjectId;
        var storageKeys = await db.TaskAttachments
            .Where(a => db.TaskItems.Any(t => t.Id == a.TaskId && t.ProjectId == projectId))
            .Select(a => a.StorageKey)
            .ToListAsync(cancellationToken);

        await db.InTransactionAsync(async ct =>
        {
            var taskIds = db.TaskItems.Where(t => t.ProjectId == projectId).Select(t => t.Id);
            await db.TaskItemTags.Where(x => taskIds.Contains(x.TaskId)).ExecuteDeleteAsync(ct);
            await db.TaskHistoryEntries.Where(x => taskIds.Contains(x.TaskId)).ExecuteDeleteAsync(ct);
            await db.TaskComments.Where(x => taskIds.Contains(x.TaskId)).ExecuteDeleteAsync(ct);
            await db.TaskAttachments.Where(x => taskIds.Contains(x.TaskId)).ExecuteDeleteAsync(ct);
            await db.Notices.Where(n => n.ProjectId == projectId).ExecuteUpdateAsync(
                s => s.SetProperty(n => n.TaskId, (Guid?)null).SetProperty(n => n.ProjectId, (Guid?)null), ct);
            await db.TaskItems.Where(t => t.ProjectId == projectId && t.ParentTaskId != null).ExecuteDeleteAsync(ct);
            await db.TaskItems.Where(t => t.ProjectId == projectId).ExecuteDeleteAsync(ct);
            await db.Tags.Where(x => x.ProjectId == projectId).ExecuteDeleteAsync(ct);
            await db.Milestones.Where(x => x.ProjectId == projectId).ExecuteDeleteAsync(ct);
            await db.WorkflowStatuses.Where(x => x.ProjectId == projectId).ExecuteDeleteAsync(ct);
            await db.ProjectMemberships.Where(x => x.ProjectId == projectId).ExecuteDeleteAsync(ct);
            await db.Projects.Where(x => x.Id == projectId).ExecuteDeleteAsync(ct);
            audit.Record(SecurityEventKind.ProjectDeleted, projectId: projectId,
                detail: new Dictionary<string, string?> { ["projectKey"] = access.ProjectKey });
            await db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        foreach (var storageKey in storageKeys)
        {
            await attachmentStorage.DeleteAsync(storageKey, cancellationToken);
        }
    }

    private async Task<string> UserTimeZoneAsync(CancellationToken cancellationToken) =>
        await db.UserAccounts.AsNoTracking().Where(u => u.Id == currentUser.UserId).Select(u => u.TimeZoneId)
            .SingleOrDefaultAsync(cancellationToken) ?? TimeZones.Default;

    private async Task<ProjectDetailResponse> BuildDetailAsync(Guid projectId, ProjectRole? role, CancellationToken cancellationToken)
    {
        var project = await db.Projects.AsNoTracking().SingleAsync(p => p.Id == projectId, cancellationToken);
        var memberCount = await db.ProjectMemberships.AsNoTracking().CountAsync(m => m.ProjectId == projectId, cancellationToken);
        var today = timeProvider.TodayIn(await UserTimeZoneAsync(cancellationToken));
        var rows = await db.TaskItems.AsNoTracking()
            .Where(t => t.ProjectId == projectId)
            .Join(db.WorkflowStatuses, t => t.StatusId, s => s.Id, (t, s) => new { s.Category, t.DueDate })
            .GroupBy(x => x.Category)
            .Select(g => new { Category = g.Key, Count = g.Count(), Overdue = g.Count(x => x.DueDate != null && x.DueDate < today) })
            .ToListAsync(cancellationToken);
        int Count(StatusCategory c) => rows.Where(r => r.Category == c).Sum(r => r.Count);
        var overdue = rows.Where(r => !r.Category.IsClosed()).Sum(r => r.Overdue);
        UserRef? createdBy = null;
        if (project.CreatedByUserId is { } creatorId)
        {
            createdBy = await db.UserAccounts.AsNoTracking().Where(u => u.Id == creatorId)
                .Select(u => new UserRef(u.Id, u.DisplayName, u.IsActive)).SingleOrDefaultAsync(cancellationToken);
        }

        return new ProjectDetailResponse(
            project.Key, project.Name, project.Description, project.IsArchived, project.ArchivedAtUtc, role, memberCount,
            new ProjectTaskCounts(Count(StatusCategory.NotStarted), Count(StatusCategory.InProgress), Count(StatusCategory.Done),
                Count(StatusCategory.Canceled), overdue),
            project.CreatedAtUtc, createdBy, project.UpdatedAtUtc, project.RowVersion);
    }
}
