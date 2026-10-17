using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Common;
using TaskManager.Domain.Common;

namespace TaskManager.Application.Security;

/// <summary>プロジェクトへのアクセス判定の結果。</summary>
public sealed record ProjectAccessContext(
    Guid ProjectId, string ProjectKey, string ProjectName, ProjectRole? Role, bool IsArchived, Guid UserId)
{
    public bool Has(ProjectPermission permission) =>
        Role is { } role && ProjectPermissionRules.IsGranted(role, permission);
}

/// <summary>
/// プロジェクト配下の操作の認可（authentication-authorization.md §7.2）。
/// 非メンバーには 404（存在を漏らさない）、ロール不足は 403、アーカイブ中の変更は 409。
/// </summary>
public sealed class ProjectAccessGuard(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<ProjectAccessContext> RequireAsync(
        string projectKey, ProjectPermission permission, CancellationToken cancellationToken)
    {
        if (!ProjectKey.TryCreate(projectKey, out var key))
        {
            throw Errors.NotFound();
        }

        var project = await db.Projects.AsNoTracking()
            .Where(p => p.Key == key.Value)
            .Select(p => new { p.Id, p.Key, p.Name, p.ArchivedAtUtc })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false) ?? throw Errors.NotFound();

        var userId = currentUser.UserId;
        var role = await db.ProjectMemberships.AsNoTracking()
            .Where(m => m.ProjectId == project.Id && m.UserId == userId)
            .Select(m => (ProjectRole?)m.Role)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var context = new ProjectAccessContext(project.Id, project.Key, project.Name, role, project.ArchivedAtUtc is not null, userId);

        if (role is null)
        {
            // システム管理者の特例: メンバー管理（管理者不在時の救済）とアーカイブ後の削除のみ
            var adminAllowed = currentUser.IsSystemAdministrator
                && permission is ProjectPermission.ManageMembers or ProjectPermission.DeleteProject;
            if (!adminAllowed)
            {
                throw Errors.NotFound();
            }

            return context;
        }

        if (permission == ProjectPermission.DeleteProject && !currentUser.IsSystemAdministrator)
        {
            throw Errors.Forbidden();
        }

        var granted = ProjectPermissionRules.IsGranted(role.Value, permission)
            || (permission == ProjectPermission.ManageMembers && currentUser.IsSystemAdministrator)
            || (permission == ProjectPermission.DeleteProject && currentUser.IsSystemAdministrator);
        if (!granted)
        {
            throw Errors.Forbidden();
        }

        if (context.IsArchived && !ProjectPermissionRules.IsAllowedWhileArchived(permission)
            && !(permission == ProjectPermission.ManageMembers && currentUser.IsSystemAdministrator))
        {
            throw Errors.ProjectArchived();
        }

        return context;
    }
}
