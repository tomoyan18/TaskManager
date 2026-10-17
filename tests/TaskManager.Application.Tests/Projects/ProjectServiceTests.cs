using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Common;
using TaskManager.Application.Projects;
using TaskManager.Application.Tasks;
using TaskManager.Domain.Common;
using TaskManager.Testing;

namespace TaskManager.Application.Tests.Projects;

public class ProjectServiceTests(DatabaseFixture fixture) : ApplicationTestBase(fixture)
{
    [Fact]
    public async Task Create_作成者が管理者になり既定のステータス5件を作る()
    {
        var user = await Data.CreateUserAsync();
        var key = Unique.ProjectKey();

        var created = await Host.RunAsync<ProjectService, ProjectDetailResponse>(user, s => s.CreateAsync(
            new CreateProjectRequest { Key = key.ToLowerInvariant(), Name = "  " + Unique.Name("営業支援") + " ", Description = "説明" }, Ct));

        Assert.Equal(key, created.Key);
        Assert.Equal(ProjectRole.Owner, created.MyRole);
        Assert.Equal(1, created.MemberCount);
        Assert.Equal(user.Id, created.CreatedBy?.Id);
        Assert.False(created.Name.StartsWith(' '));
        var statuses = await Host.RunAsync<WorkflowStatusService, IReadOnlyList<WorkflowStatusResponse>>(user, s => s.ListAsync(key, Ct));
        Assert.Equal(["未着手", "対応中", "確認待ち", "完了", "中止"], statuses.Select(s => s.Name).ToArray());
    }

    [Fact]
    public async Task Create_キーと名前の重複は409()
    {
        var user = await Data.CreateUserAsync();
        var existing = await Data.CreateProjectAsync(user);

        await AppAssert.ThrowsAsync(AppErrorKind.Conflict, ErrorCodes.ProjectKeyTaken, () => Host.RunAsync<ProjectService, ProjectDetailResponse>(
            user, s => s.CreateAsync(new CreateProjectRequest { Key = existing.Key.ToLowerInvariant(), Name = Unique.Name("別名") }, Ct)));
        await AppAssert.ThrowsAsync(AppErrorKind.Conflict, ErrorCodes.ProjectNameTaken, () => Host.RunAsync<ProjectService, ProjectDetailResponse>(
            user, s => s.CreateAsync(new CreateProjectRequest { Key = Unique.ProjectKey(), Name = existing.Name }, Ct)));
    }

    [Theory]
    [InlineData("1ABC")]
    [InlineData("A")]
    public async Task Create_キーの形式が不正なら項目エラー(string key)
    {
        var user = await Data.CreateUserAsync();
        await AppAssert.ValidationAsync("key", () => Host.RunAsync<ProjectService, ProjectDetailResponse>(
            user, s => s.CreateAsync(new CreateProjectRequest { Key = key, Name = Unique.Name("名前") }, Ct)));
    }

    [Fact]
    public async Task Update_古い版での更新は409で内容は変わらない()
    {
        var owner = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        var detail = await Host.RunAsync<ProjectService, ProjectDetailResponse>(owner, s => s.GetAsync(project.Key, Ct));

        var newName = Unique.Name("新しい名前");
        var updated = await Host.RunAsync<ProjectService, ProjectDetailResponse>(owner, s => s.UpdateAsync(
            project.Key, new UpdateProjectRequest { Name = newName, Version = detail.Version }, Ct));
        Assert.Equal(newName, updated.Name);
        Assert.Equal(detail.Version + 1, updated.Version);

        await AppAssert.ThrowsAsync(AppErrorKind.Conflict, ErrorCodes.ConcurrencyConflict, () => Host.RunAsync<ProjectService, ProjectDetailResponse>(
            owner, s => s.UpdateAsync(project.Key, new UpdateProjectRequest { Name = Unique.Name("上書き"), Version = detail.Version }, Ct)));
        var current = await Host.RunAsync<ProjectService, ProjectDetailResponse>(owner, s => s.GetAsync(project.Key, Ct));
        Assert.Equal(newName, current.Name);
    }

    [Fact]
    public async Task Update_送らなかった項目は変更しない()
    {
        var owner = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        var detail = await Host.RunAsync<ProjectService, ProjectDetailResponse>(owner, s => s.GetAsync(project.Key, Ct));
        await Host.RunAsync<ProjectService, ProjectDetailResponse>(owner, s => s.UpdateAsync(
            project.Key, new UpdateProjectRequest { Description = "説明を追加", Version = detail.Version }, Ct));

        var current = await Host.RunAsync<ProjectService, ProjectDetailResponse>(owner, s => s.GetAsync(project.Key, Ct));
        Assert.Equal(project.Name, current.Name);
        Assert.Equal("説明を追加", current.Description);
    }

    [Fact]
    public async Task Update_編集者は設定を変更できない()
    {
        var owner = await Data.CreateUserAsync();
        var editor = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        await Data.AddMemberAsync(project, editor, ProjectRole.Editor);

        await AppAssert.ForbiddenAsync(() => Host.RunAsync<ProjectService, ProjectDetailResponse>(
            editor, s => s.UpdateAsync(project.Key, new UpdateProjectRequest { Name = "変更", Version = 0 }, Ct)));
    }

    [Fact]
    public async Task List_自分が所属するプロジェクトだけを返す()
    {
        var me = await Data.CreateUserAsync();
        var other = await Data.CreateUserAsync();
        var mine = await Data.CreateProjectAsync(me);
        var others = await Data.CreateProjectAsync(other);

        var list = await Host.RunAsync<ProjectService, PagedResponse<ProjectSummaryResponse>>(
            me, s => s.ListAsync(false, null, null, allScope: true, PageRequest.Of(1, 100, 20), Ct));

        Assert.Equal(mine.Key, Assert.Single(list.Items).Key);
        Assert.DoesNotContain(list.Items, p => p.Key == others.Key);
    }

    [Fact]
    public async Task List_システム管理者は全件指定で所属外も参照できる()
    {
        var admin = await Data.CreateUserAsync(administrator: true);
        var other = await Data.CreateUserAsync();
        var key = Unique.ProjectKey();
        var project = await Data.CreateProjectAsync(other, key: key);
        await Data.CreateTaskAsync(project, other);

        var list = await Host.RunAsync<ProjectService, PagedResponse<ProjectSummaryResponse>>(
            admin, s => s.ListAsync(false, key, null, allScope: true, PageRequest.Of(1, 100, 20), Ct));

        var item = Assert.Single(list.Items);
        Assert.Null(item.MyRole);
        Assert.Equal(0, item.OpenTaskCount); // 参加していないプロジェクトの件数は返さない
    }

    [Fact]
    public async Task Archive_アーカイブと解除()
    {
        var owner = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        var archived = await Host.RunAsync<ProjectService, ProjectDetailResponse>(owner, s => s.ArchiveAsync(project.Key, true, Ct));
        Assert.True(archived.IsArchived);
        await AppAssert.ThrowsAsync(AppErrorKind.Conflict, ErrorCodes.ProjectArchived, () => Host.RunAsync<TaskItemService, TaskDetailResponse>(
            owner, s => s.CreateAsync(project.Key, new CreateTaskRequest { Title = "追加できない" }, Ct)));
        var restored = await Host.RunAsync<ProjectService, ProjectDetailResponse>(owner, s => s.ArchiveAsync(project.Key, false, Ct));
        Assert.False(restored.IsArchived);
    }

    [Fact]
    public async Task Delete_アーカイブ済みでキー確認が一致すれば関連データとファイルを削除する()
    {
        var admin = await Data.CreateUserAsync(administrator: true);
        var owner = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        var task = await Data.CreateTaskAsync(project, owner);
        await Host.RunAsync<TaskCommentService, TaskCommentResponse>(owner, s => s.CreateAsync(
            project.Key, task.Number, new CreateTaskCommentRequest { Body = "コメント" }, Ct));
        using var file = new MemoryStream("見積もりメモ"u8.ToArray());
        await Host.RunAsync<TaskAttachmentService, TaskAttachmentResponse>(owner, s => s.UploadAsync(
            project.Key, task.Number, "memo.txt", file, file.Length, Ct));
        Assert.NotEmpty(Directory.EnumerateFiles(Host.AttachmentRoot, "*", SearchOption.AllDirectories));

        await AppAssert.ThrowsAsync(AppErrorKind.Conflict, ErrorCodes.ProjectNotArchived,
            () => Host.RunAsync<ProjectService>(admin, s => s.DeleteAsync(project.Key, project.Key, Ct)));
        await Data.ArchiveProjectAsync(project);
        await AppAssert.ValidationAsync("confirmKey",
            () => Host.RunAsync<ProjectService>(admin, s => s.DeleteAsync(project.Key, "WRONG", Ct)));
        await AppAssert.ForbiddenAsync(() => Host.RunAsync<ProjectService>(owner, s => s.DeleteAsync(project.Key, project.Key, Ct)));

        await Host.RunAsync<ProjectService>(admin, s => s.DeleteAsync(project.Key, project.Key.ToLowerInvariant(), Ct));

        Assert.False(await Data.QueryAsync(db => db.Projects.AnyAsync(p => p.Id == project.Id, Ct)));
        Assert.False(await Data.QueryAsync(db => db.TaskItems.AnyAsync(t => t.ProjectId == project.Id, Ct)));
        Assert.False(await Data.QueryAsync(db => db.TaskComments.AnyAsync(c => c.TaskId == task.Id, Ct)));
        Assert.False(await Data.QueryAsync(db => db.WorkflowStatuses.AnyAsync(s => s.ProjectId == project.Id, Ct)));
        Assert.Empty(Directory.EnumerateFiles(Host.AttachmentRoot, "*", SearchOption.AllDirectories));
        Assert.True(await Data.QueryAsync(db => db.SecurityAuditEvents.AnyAsync(
            e => e.ProjectId == project.Id && e.Kind == SecurityEventKind.ProjectDeleted, Ct)));
    }
}
