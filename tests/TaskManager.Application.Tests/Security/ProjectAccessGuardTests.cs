using TaskManager.Application.Common;
using TaskManager.Application.Security;
using TaskManager.Domain.Common;
using TaskManager.Testing;

namespace TaskManager.Application.Tests.Security;

/// <summary>プロジェクト配下の認可（authentication-authorization.md §7.2）: 非メンバー 404、ロール不足 403、アーカイブ中の変更 409。</summary>
public class ProjectAccessGuardTests(DatabaseFixture fixture) : ApplicationTestBase(fixture)
{
    private Task<ProjectAccessContext> RequireAsync(TestUser user, string key, ProjectPermission permission) =>
        Host.RunAsync<ProjectAccessGuard, ProjectAccessContext>(user, g => g.RequireAsync(key, permission, Ct));

    [Fact]
    public async Task 非メンバーには存在を明かさず404()
    {
        var owner = await Data.CreateUserAsync();
        var outsider = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);

        await AppAssert.NotFoundAsync(() => RequireAsync(outsider, project.Key, ProjectPermission.ViewProject));
        await AppAssert.NotFoundAsync(() => RequireAsync(outsider, project.Key, ProjectPermission.EditTasks));
    }

    [Theory]
    [InlineData("NOSUCHKEY")]
    [InlineData("invalid-key")]
    [InlineData("../etc")]
    public async Task 存在しないキーや不正なキーは404(string key)
    {
        var user = await Data.CreateUserAsync();
        await AppAssert.NotFoundAsync(() => RequireAsync(user, key, ProjectPermission.ViewProject));
    }

    [Theory]
    [InlineData(ProjectRole.Viewer, ProjectPermission.ViewProject, true)]
    [InlineData(ProjectRole.Viewer, ProjectPermission.EditTasks, false)]
    [InlineData(ProjectRole.Viewer, ProjectPermission.Comment, false)]
    [InlineData(ProjectRole.Editor, ProjectPermission.EditTasks, true)]
    [InlineData(ProjectRole.Editor, ProjectPermission.ManageStatuses, false)]
    [InlineData(ProjectRole.Editor, ProjectPermission.ManageMembers, false)]
    [InlineData(ProjectRole.Owner, ProjectPermission.ManageMembers, true)]
    [InlineData(ProjectRole.Owner, ProjectPermission.DeleteProject, false)]
    public async Task メンバーはロールに応じて許可または403(ProjectRole role, ProjectPermission permission, bool allowed)
    {
        var owner = await Data.CreateUserAsync();
        var member = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        await Data.AddMemberAsync(project, member, role);

        if (allowed)
        {
            var context = await RequireAsync(member, project.Key.ToLowerInvariant(), permission);
            Assert.Equal(project.Id, context.ProjectId);
            Assert.Equal(role, context.Role);
        }
        else
        {
            await AppAssert.ForbiddenAsync(() => RequireAsync(member, project.Key, permission));
        }
    }

    [Fact]
    public async Task アーカイブ中は参照できるが変更は409()
    {
        var owner = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        await Data.ArchiveProjectAsync(project);

        var context = await RequireAsync(owner, project.Key, ProjectPermission.ViewProject);
        Assert.True(context.IsArchived);
        await RequireAsync(owner, project.Key, ProjectPermission.ArchiveProject);
        await AppAssert.ThrowsAsync(AppErrorKind.Conflict, ErrorCodes.ProjectArchived,
            () => RequireAsync(owner, project.Key, ProjectPermission.EditTasks));
        await AppAssert.ThrowsAsync(AppErrorKind.Conflict, ErrorCodes.ProjectArchived,
            () => RequireAsync(owner, project.Key, ProjectPermission.ManageMembers));
    }

    [Fact]
    public async Task システム管理者は非メンバーでもメンバー管理と削除だけ可能()
    {
        var owner = await Data.CreateUserAsync();
        var admin = await Data.CreateUserAsync(administrator: true);
        var project = await Data.CreateProjectAsync(owner);

        await AppAssert.NotFoundAsync(() => RequireAsync(admin, project.Key, ProjectPermission.ViewProject));
        await AppAssert.NotFoundAsync(() => RequireAsync(admin, project.Key, ProjectPermission.EditTasks));
        var members = await RequireAsync(admin, project.Key, ProjectPermission.ManageMembers);
        Assert.Null(members.Role);
        await RequireAsync(admin, project.Key, ProjectPermission.DeleteProject);
    }

    [Fact]
    public async Task システム管理者はアーカイブ中でもメンバー管理できる()
    {
        var owner = await Data.CreateUserAsync();
        var admin = await Data.CreateUserAsync(administrator: true);
        var project = await Data.CreateProjectAsync(owner);
        await Data.AddMemberAsync(project, admin, ProjectRole.Viewer);
        await Data.ArchiveProjectAsync(project);

        await RequireAsync(admin, project.Key, ProjectPermission.ManageMembers);
    }
}
