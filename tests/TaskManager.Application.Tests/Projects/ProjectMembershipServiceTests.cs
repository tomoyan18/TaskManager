using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Common;
using TaskManager.Application.Projects;
using TaskManager.Domain.Common;
using TaskManager.Testing;

namespace TaskManager.Application.Tests.Projects;

public class ProjectMembershipServiceTests(DatabaseFixture fixture) : ApplicationTestBase(fixture)
{
    [Fact]
    public async Task Add_メンバーを追加し本人にお知らせする()
    {
        var owner = await Data.CreateUserAsync();
        var user = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);

        var member = await Host.RunAsync<ProjectMembershipService, ProjectMemberResponse>(owner, s => s.AddAsync(
            project.Key, new AddProjectMemberRequest { UserId = user.Id, Role = ProjectRole.Editor }, Ct));

        Assert.Equal(ProjectRole.Editor, member.Role);
        var notice = await Data.QueryAsync(db => db.Notices.SingleAsync(n => n.RecipientUserId == user.Id, Ct));
        Assert.Equal(NoticeKind.AddedToProject, notice.Kind);
        Assert.Equal($"/projects/{project.Key}/tasks", notice.LinkPath);
        Assert.Contains(project.Name, notice.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Add_既存メンバー_停止中_存在しない利用者を拒否する()
    {
        var owner = await Data.CreateUserAsync();
        var inactive = await Data.CreateUserAsync(active: false);
        var project = await Data.CreateProjectAsync(owner);

        await AppAssert.ThrowsAsync(AppErrorKind.Conflict, ErrorCodes.MemberAlreadyExists, () => Host.RunAsync<ProjectMembershipService, ProjectMemberResponse>(
            owner, s => s.AddAsync(project.Key, new AddProjectMemberRequest { UserId = owner.Id, Role = ProjectRole.Viewer }, Ct)));
        await AppAssert.ThrowsAsync(AppErrorKind.BadRequest, ErrorCodes.MemberUserInactive, () => Host.RunAsync<ProjectMembershipService, ProjectMemberResponse>(
            owner, s => s.AddAsync(project.Key, new AddProjectMemberRequest { UserId = inactive.Id, Role = ProjectRole.Viewer }, Ct)));
        await AppAssert.ValidationAsync("userId", () => Host.RunAsync<ProjectMembershipService, ProjectMemberResponse>(
            owner, s => s.AddAsync(project.Key, new AddProjectMemberRequest { UserId = Guid.NewGuid(), Role = ProjectRole.Viewer }, Ct)));
    }

    [Fact]
    public async Task Add_編集者はメンバーを追加できない()
    {
        var owner = await Data.CreateUserAsync();
        var editor = await Data.CreateUserAsync();
        var user = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        await Data.AddMemberAsync(project, editor, ProjectRole.Editor);

        await AppAssert.ForbiddenAsync(() => Host.RunAsync<ProjectMembershipService, ProjectMemberResponse>(
            editor, s => s.AddAsync(project.Key, new AddProjectMemberRequest { UserId = user.Id, Role = ProjectRole.Owner }, Ct)));
    }

    [Fact]
    public async Task ChangeRole_閲覧者にすると担当を外し履歴を残す()
    {
        var owner = await Data.CreateUserAsync();
        var editor = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        await Data.AddMemberAsync(project, editor, ProjectRole.Editor);
        var task = await Data.CreateTaskAsync(project, owner, assigneeUserId: editor.Id);

        await Host.RunAsync<ProjectMembershipService, ProjectMemberResponse>(owner, s => s.ChangeRoleAsync(
            project.Key, editor.Id, new ChangeProjectMemberRoleRequest { Role = ProjectRole.Viewer }, Ct));

        var reloaded = await Data.QueryAsync(db => db.TaskItems.SingleAsync(t => t.Id == task.Id, Ct));
        Assert.Null(reloaded.AssigneeUserId);
        var history = await Data.QueryAsync(db => db.TaskHistoryEntries.SingleAsync(
            h => h.TaskId == task.Id && h.Kind == TaskHistoryKind.AssigneeChanged, Ct));
        Assert.Null(history.ActorUserId);
        Assert.Equal(editor.DisplayName, history.OldValue);
    }

    [Fact]
    public async Task 最後の管理者は降格も脱退もできない()
    {
        var owner = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);

        await AppAssert.ThrowsAsync(AppErrorKind.Conflict, ErrorCodes.MemberLastOwner, () => Host.RunAsync<ProjectMembershipService, ProjectMemberResponse>(
            owner, s => s.ChangeRoleAsync(project.Key, owner.Id, new ChangeProjectMemberRoleRequest { Role = ProjectRole.Editor }, Ct)));
        await AppAssert.ThrowsAsync(AppErrorKind.Conflict, ErrorCodes.MemberLastOwner,
            () => Host.RunAsync<ProjectMembershipService>(owner, s => s.RemoveAsync(project.Key, owner.Id, Ct)));
    }

    [Fact]
    public async Task Remove_閲覧者は自分で脱退できるが他人は外せない()
    {
        var owner = await Data.CreateUserAsync();
        var viewer = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        await Data.AddMemberAsync(project, viewer, ProjectRole.Viewer);

        await AppAssert.ForbiddenAsync(() => Host.RunAsync<ProjectMembershipService>(viewer, s => s.RemoveAsync(project.Key, owner.Id, Ct)));
        await Host.RunAsync<ProjectMembershipService>(viewer, s => s.RemoveAsync(project.Key, viewer.Id, Ct));

        Assert.False(await Data.QueryAsync(db => db.ProjectMemberships.AnyAsync(m => m.ProjectId == project.Id && m.UserId == viewer.Id, Ct)));
        await AppAssert.NotFoundAsync(() => Host.RunAsync<ProjectMembershipService, IReadOnlyList<ProjectMemberResponse>>(
            viewer, s => s.ListAsync(project.Key, null, false, Ct)));
    }

    [Fact]
    public async Task Remove_外すと担当を外す()
    {
        var owner = await Data.CreateUserAsync();
        var editor = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        await Data.AddMemberAsync(project, editor, ProjectRole.Editor);
        var task = await Data.CreateTaskAsync(project, owner, assigneeUserId: editor.Id);

        await Host.RunAsync<ProjectMembershipService>(owner, s => s.RemoveAsync(project.Key, editor.Id, Ct));

        Assert.Null((await Data.QueryAsync(db => db.TaskItems.SingleAsync(t => t.Id == task.Id, Ct))).AssigneeUserId);
    }

    [Fact]
    public async Task システム管理者は管理者不在のプロジェクトにメンバーを追加できる()
    {
        var owner = await Data.CreateUserAsync();
        var admin = await Data.CreateUserAsync(administrator: true);
        var rescuer = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);

        await Host.RunAsync<ProjectMembershipService, ProjectMemberResponse>(admin, s => s.AddAsync(
            project.Key, new AddProjectMemberRequest { UserId = rescuer.Id, Role = ProjectRole.Owner }, Ct));

        Assert.True(await Data.QueryAsync(db => db.SecurityAuditEvents.AnyAsync(
            e => e.Kind == SecurityEventKind.MembershipAdded && e.SubjectUserId == rescuer.Id && e.ActorUserId == admin.Id, Ct)));
    }
}
