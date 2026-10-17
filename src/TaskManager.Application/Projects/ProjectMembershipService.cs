using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Common;
using TaskManager.Application.Notices;
using TaskManager.Application.Security;
using TaskManager.Domain.Common;
using TaskManager.Domain.Projects;
using TaskManager.Domain.Tasks;

namespace TaskManager.Application.Projects;

/// <summary>プロジェクトメンバー（REQ-MEM-01〜04）。</summary>
public sealed class ProjectMembershipService(
    IAppDbContext db,
    ICurrentUser currentUser,
    ProjectAccessGuard guard,
    NoticeWriter noticeWriter,
    SecurityAuditService audit,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<ProjectMemberResponse>> ListAsync(
        string projectKey, ProjectRole? role, bool assignableOnly, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.ViewProject, cancellationToken);
        var query = db.ProjectMemberships.AsNoTracking().Where(m => m.ProjectId == access.ProjectId);
        if (role is { } r)
        {
            query = query.Where(m => m.Role == r);
        }

        if (assignableOnly)
        {
            query = query.Where(m => m.Role >= ProjectRole.Editor);
        }

        return await query
            .Join(db.UserAccounts, m => m.UserId, u => u.Id, (m, u) => new { m, u })
            .Where(x => !assignableOnly || x.u.IsActive)
            .OrderBy(x => x.u.DisplayName)
            .Select(x => new ProjectMemberResponse(
                new UserRef(x.u.Id, x.u.DisplayName, x.u.IsActive), x.u.Email, x.m.Role, x.m.JoinedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProjectMemberResponse> AddAsync(string projectKey, AddProjectMemberRequest request, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.ManageMembers, cancellationToken);
        var userId = request.UserId!.Value;
        var role = request.Role!.Value;
        var user = await db.UserAccounts.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw Errors.Validation("userId", "選択した利用者が見つかりません。");
        if (!user.IsActive)
        {
            throw new AppException(AppErrorKind.BadRequest, ErrorCodes.MemberUserInactive, "利用停止中の利用者は追加できません。",
                errors: new Dictionary<string, string[]> { ["userId"] = ["利用停止中の利用者は追加できません。"] });
        }

        if (await db.ProjectMemberships.AnyAsync(m => m.ProjectId == access.ProjectId && m.UserId == userId, cancellationToken))
        {
            throw Errors.Conflict(ErrorCodes.MemberAlreadyExists, "この利用者は既にメンバーです。");
        }

        var now = timeProvider.UtcNow();
        var membership = ProjectMembership.Create(access.ProjectId, userId, role, currentUser.UserId, now);
        db.ProjectMemberships.Add(membership);
        var actorName = await ActorNameAsync(cancellationToken);
        await noticeWriter.NotifyAsync(userId, NoticeKind.AddedToProject, currentUser.UserId, access.ProjectId, null,
            $"{actorName}さんがあなたをプロジェクト「{access.ProjectName}」に追加しました",
            $"/projects/{access.ProjectKey}/tasks", cancellationToken);
        audit.Record(SecurityEventKind.MembershipAdded, userId, access.ProjectId,
            new Dictionary<string, string?> { ["role"] = role.ToString() });
        await db.SaveChangesAsync(cancellationToken);
        return new ProjectMemberResponse(new UserRef(user.Id, user.DisplayName, user.IsActive), user.Email, role, now);
    }

    public async Task<ProjectMemberResponse> ChangeRoleAsync(
        string projectKey, Guid userId, ChangeProjectMemberRoleRequest request, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.ManageMembers, cancellationToken);
        var role = request.Role!.Value;
        var membership = await db.ProjectMemberships
            .SingleOrDefaultAsync(m => m.ProjectId == access.ProjectId && m.UserId == userId, cancellationToken)
            ?? throw Errors.NotFound();
        var user = await db.UserAccounts.AsNoTracking().SingleAsync(u => u.Id == userId, cancellationToken);

        if (membership.Role != role)
        {
            if (membership.Role == ProjectRole.Owner)
            {
                await EnsureAnotherOwnerAsync(access.ProjectId, userId, cancellationToken);
            }

            var now = timeProvider.UtcNow();
            if (!role.CanBeAssigned())
            {
                await UnassignTasksAsync(access.ProjectId, userId, user.DisplayName, now, cancellationToken);
            }

            membership.ChangeRole(role, currentUser.UserId, now);
            audit.Record(SecurityEventKind.MembershipRoleChanged, userId, access.ProjectId,
                new Dictionary<string, string?> { ["role"] = role.ToString() });
            await db.SaveChangesAsync(cancellationToken);
        }

        return new ProjectMemberResponse(new UserRef(user.Id, user.DisplayName, user.IsActive), user.Email, membership.Role, membership.JoinedAtUtc);
    }

    public async Task RemoveAsync(string projectKey, Guid userId, CancellationToken cancellationToken)
    {
        var isSelf = userId == currentUser.UserId;
        var access = await guard.RequireAsync(
            projectKey, isSelf ? ProjectPermission.LeaveProject : ProjectPermission.ManageMembers, cancellationToken);
        if (isSelf && access.IsArchived)
        {
            throw Errors.ProjectArchived();
        }

        var membership = await db.ProjectMemberships
            .SingleOrDefaultAsync(m => m.ProjectId == access.ProjectId && m.UserId == userId, cancellationToken)
            ?? throw Errors.NotFound();
        if (membership.Role == ProjectRole.Owner)
        {
            await EnsureAnotherOwnerAsync(access.ProjectId, userId, cancellationToken);
        }

        var displayName = await db.UserAccounts.AsNoTracking().Where(u => u.Id == userId).Select(u => u.DisplayName)
            .SingleAsync(cancellationToken);
        await UnassignTasksAsync(access.ProjectId, userId, displayName, timeProvider.UtcNow(), cancellationToken);
        db.ProjectMemberships.Remove(membership);
        audit.Record(SecurityEventKind.MembershipRemoved, userId, access.ProjectId);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>M-02: 担当を外し、システムによる変更として履歴を残す。</summary>
    private async Task UnassignTasksAsync(Guid projectId, Guid userId, string displayName, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var tasks = await db.TaskItems.Where(t => t.ProjectId == projectId && t.AssigneeUserId == userId).ToListAsync(cancellationToken);
        foreach (var task in tasks)
        {
            task.AssignTo(null);
            db.TaskHistoryEntries.Add(TaskHistoryEntry.Create(task.Id, TaskHistoryKind.AssigneeChanged, null, displayName, null, nowUtc));
        }
    }

    private async Task EnsureAnotherOwnerAsync(Guid projectId, Guid exceptUserId, CancellationToken cancellationToken)
    {
        var hasOther = await db.ProjectMemberships.AnyAsync(
            m => m.ProjectId == projectId && m.UserId != exceptUserId && m.Role == ProjectRole.Owner, cancellationToken);
        if (!hasOther)
        {
            throw Errors.Conflict(ErrorCodes.MemberLastOwner, "管理者が 1 人もいなくなるため、この操作はできません。",
                "先に別のメンバーを管理者にしてください。");
        }
    }

    private async Task<string> ActorNameAsync(CancellationToken cancellationToken) =>
        await db.UserAccounts.AsNoTracking().Where(u => u.Id == currentUser.UserId).Select(u => u.DisplayName)
            .SingleOrDefaultAsync(cancellationToken) ?? string.Empty;
}
