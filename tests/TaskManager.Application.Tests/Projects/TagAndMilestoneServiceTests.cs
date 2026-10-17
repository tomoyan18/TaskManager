using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Common;
using TaskManager.Application.Projects;
using TaskManager.Application.Tasks;
using TaskManager.Domain.Common;
using TaskManager.Testing;

namespace TaskManager.Application.Tests.Projects;

public class TagServiceTests(DatabaseFixture fixture) : ApplicationTestBase(fixture)
{
    [Fact]
    public async Task Create_大文字小文字を区別せず同名は409()
    {
        var owner = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        await Host.RunAsync<TagService, TagResponse>(owner, s => s.CreateAsync(project.Key, new CreateTagRequest { Name = "Design", Color = StatusColor.Blue }, Ct));

        await AppAssert.ThrowsAsync(AppErrorKind.Conflict, ErrorCodes.TagNameTaken, () => Host.RunAsync<TagService, TagResponse>(
            owner, s => s.CreateAsync(project.Key, new CreateTagRequest { Name = "design", Color = StatusColor.Red }, Ct)));
    }

    [Fact]
    public async Task Delete_タスクからも外す()
    {
        var owner = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        var tag = await Data.CreateTagAsync(project);
        var task = await Data.CreateTaskAsync(project, owner);
        var detail = await Host.RunAsync<TaskItemService, TaskDetailResponse>(owner, s => s.GetAsync(project.Key, task.Number, Ct));
        await Host.RunAsync<TaskItemService, TaskDetailResponse>(owner, s => s.UpdateAsync(
            project.Key, task.Number, new UpdateTaskRequest { TagIds = [tag.Id], Version = detail.Version }, Ct));

        await Host.RunAsync<TagService>(owner, s => s.DeleteAsync(project.Key, tag.Id, Ct));

        Assert.False(await Data.QueryAsync(db => db.TaskItemTags.AnyAsync(x => x.TagId == tag.Id, Ct)));
    }

    [Fact]
    public async Task Delete_別プロジェクトのタグIDは404で削除されない()
    {
        var owner = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        var other = await Data.CreateProjectAsync(await Data.CreateUserAsync());
        var foreign = await Data.CreateTagAsync(other);

        await AppAssert.NotFoundAsync(() => Host.RunAsync<TagService>(owner, s => s.DeleteAsync(project.Key, foreign.Id, Ct)));
        Assert.True(await Data.QueryAsync(db => db.Tags.AnyAsync(t => t.Id == foreign.Id, Ct)));
    }

    [Fact]
    public async Task 閲覧者はタグを作れない()
    {
        var owner = await Data.CreateUserAsync();
        var viewer = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        await Data.AddMemberAsync(project, viewer, ProjectRole.Viewer);

        await AppAssert.ForbiddenAsync(() => Host.RunAsync<TagService, TagResponse>(
            viewer, s => s.CreateAsync(project.Key, new CreateTagRequest { Name = "閲覧者", Color = StatusColor.Gray }, Ct)));
    }
}

public class MilestoneServiceTests(DatabaseFixture fixture) : ApplicationTestBase(fixture)
{
    [Fact]
    public async Task Create_同名は409()
    {
        var owner = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        var created = await Host.RunAsync<MilestoneService, MilestoneDetailResponse>(owner, s => s.CreateAsync(project.Key,
            new CreateMilestoneRequest { Name = "10月リリース", TargetDate = new DateOnly(2026, 10, 31) }, Ct));
        Assert.Equal(MilestoneState.Open, created.State);

        await AppAssert.ThrowsAsync(AppErrorKind.Conflict, ErrorCodes.MilestoneNameTaken, () => Host.RunAsync<MilestoneService, MilestoneDetailResponse>(
            owner, s => s.CreateAsync(project.Key, new CreateMilestoneRequest { Name = "10月リリース" }, Ct)));
    }

    [Fact]
    public async Task Update_古い版は409()
    {
        var owner = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        var milestone = await Data.CreateMilestoneAsync(project);

        var first = await Host.RunAsync<MilestoneService, MilestoneDetailResponse>(owner, s => s.UpdateAsync(
            project.Key, milestone.Id, new UpdateMilestoneRequest { State = MilestoneState.Closed, Version = 0 }, Ct));
        Assert.Equal(MilestoneState.Closed, first.State);
        await AppAssert.ThrowsAsync(AppErrorKind.Conflict, ErrorCodes.ConcurrencyConflict, () => Host.RunAsync<MilestoneService, MilestoneDetailResponse>(
            owner, s => s.UpdateAsync(project.Key, milestone.Id, new UpdateMilestoneRequest { State = MilestoneState.Open, Version = 0 }, Ct)));
    }

    [Fact]
    public async Task Delete_タスクとの関連を外して履歴を残す()
    {
        var owner = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        var milestone = await Data.CreateMilestoneAsync(project, "第1期");
        var task = await Data.CreateTaskAsync(project, owner);
        await Host.RunAsync<TaskItemService, TaskDetailResponse>(owner, s => s.UpdateAsync(
            project.Key, task.Number, new UpdateTaskRequest { MilestoneId = milestone.Id, Version = 0 }, Ct));

        await Host.RunAsync<MilestoneService>(owner, s => s.DeleteAsync(project.Key, milestone.Id, Ct));

        Assert.Null((await Data.QueryAsync(db => db.TaskItems.SingleAsync(t => t.Id == task.Id, Ct))).MilestoneId);
        var history = await Data.QueryAsync(db => db.TaskHistoryEntries
            .Where(h => h.TaskId == task.Id && h.Kind == TaskHistoryKind.MilestoneChanged).OrderBy(h => h.OccurredAtUtc).ToListAsync(Ct));
        Assert.Equal(("第1期", (string?)null), (history[^1].OldValue, history[^1].NewValue));
    }
}
