using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Common;
using TaskManager.Application.Projects;
using TaskManager.Domain.Common;
using TaskManager.Testing;

namespace TaskManager.Application.Tests.Projects;

public class WorkflowStatusServiceTests(DatabaseFixture fixture) : ApplicationTestBase(fixture)
{
    [Fact]
    public async Task Create_末尾に追加し同名は409()
    {
        var owner = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);

        var created = await Host.RunAsync<WorkflowStatusService, WorkflowStatusResponse>(owner, s => s.CreateAsync(project.Key,
            new CreateWorkflowStatusRequest { Name = "保留", Category = StatusCategory.InProgress, Color = StatusColor.Yellow }, Ct));
        Assert.Equal(6, created.SortOrder);

        await AppAssert.ThrowsAsync(AppErrorKind.Conflict, ErrorCodes.StatusNameTaken, () => Host.RunAsync<WorkflowStatusService, WorkflowStatusResponse>(
            owner, s => s.CreateAsync(project.Key, new CreateWorkflowStatusRequest { Name = "完了", Category = StatusCategory.Done, Color = StatusColor.Green }, Ct)));
    }

    [Fact]
    public async Task Create_20件を超えると409()
    {
        var owner = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        for (var i = 6; i <= 20; i++)
        {
            var name = $"ステータス{i}";
            await Host.RunAsync<WorkflowStatusService, WorkflowStatusResponse>(owner, s => s.CreateAsync(project.Key,
                new CreateWorkflowStatusRequest { Name = name, Category = StatusCategory.InProgress, Color = StatusColor.Gray }, Ct));
        }

        await AppAssert.ThrowsAsync(AppErrorKind.Conflict, ErrorCodes.StatusLimitReached, () => Host.RunAsync<WorkflowStatusService, WorkflowStatusResponse>(
            owner, s => s.CreateAsync(project.Key, new CreateWorkflowStatusRequest { Name = "21件目", Category = StatusCategory.InProgress, Color = StatusColor.Gray }, Ct)));
    }

    [Fact]
    public async Task Delete_既定のステータスは削除できない()
    {
        var owner = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        var notStarted = await Data.GetStatusAsync(project, StatusCategory.NotStarted);

        await AppAssert.ThrowsAsync(AppErrorKind.Conflict, ErrorCodes.StatusIsDefault,
            () => Host.RunAsync<WorkflowStatusService>(owner, s => s.DeleteAsync(project.Key, notStarted.Id, null, Ct)));
    }

    [Fact]
    public async Task Delete_使用中は移動先が必要で移動後に履歴を残す()
    {
        var owner = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        var done = await Data.GetStatusAsync(project, StatusCategory.Done);
        var canceled = await Data.GetStatusAsync(project, StatusCategory.Canceled);
        var task = await Data.CreateTaskAsync(project, owner, category: StatusCategory.Canceled);

        await AppAssert.ThrowsAsync(AppErrorKind.Conflict, ErrorCodes.StatusInUse,
            () => Host.RunAsync<WorkflowStatusService>(owner, s => s.DeleteAsync(project.Key, canceled.Id, null, Ct)));
        await AppAssert.ValidationAsync("moveTasksToStatusId",
            () => Host.RunAsync<WorkflowStatusService>(owner, s => s.DeleteAsync(project.Key, canceled.Id, canceled.Id, Ct)));

        await Host.RunAsync<WorkflowStatusService>(owner, s => s.DeleteAsync(project.Key, canceled.Id, done.Id, Ct));

        var moved = await Data.QueryAsync(db => db.TaskItems.SingleAsync(t => t.Id == task.Id, Ct));
        Assert.Equal(done.Id, moved.StatusId);
        Assert.NotNull(moved.CompletedAtUtc);
        var history = await Data.QueryAsync(db => db.TaskHistoryEntries.SingleAsync(
            h => h.TaskId == task.Id && h.Kind == TaskHistoryKind.StatusChanged, Ct));
        Assert.Equal(("中止", "完了"), (history.OldValue, history.NewValue));
        var remaining = await Data.GetStatusesAsync(project);
        Assert.Equal([1, 2, 3, 4], remaining.Select(s => (int)s.SortOrder).ToArray());
    }

    [Fact]
    public async Task Delete_移動先に別プロジェクトのステータスは指定できない()
    {
        var owner = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        var other = await Data.CreateProjectAsync(owner);
        var canceled = await Data.GetStatusAsync(project, StatusCategory.Canceled);
        var foreign = await Data.GetStatusAsync(other, StatusCategory.Done);
        await Data.CreateTaskAsync(project, owner, category: StatusCategory.Canceled);

        await AppAssert.ValidationAsync("moveTasksToStatusId",
            () => Host.RunAsync<WorkflowStatusService>(owner, s => s.DeleteAsync(project.Key, canceled.Id, foreign.Id, Ct)));
        Assert.True(await Data.QueryAsync(db => db.WorkflowStatuses.AnyAsync(s => s.Id == canceled.Id, Ct)));
    }

    [Fact]
    public async Task Delete_別プロジェクトのステータスIDは404()
    {
        var owner = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        var other = await Data.CreateProjectAsync(owner);
        var foreign = await Data.GetStatusAsync(other, StatusCategory.Canceled);

        await AppAssert.NotFoundAsync(() => Host.RunAsync<WorkflowStatusService>(owner, s => s.DeleteAsync(project.Key, foreign.Id, null, Ct)));
        Assert.True(await Data.QueryAsync(db => db.WorkflowStatuses.AnyAsync(s => s.Id == foreign.Id, Ct)));
    }

    [Fact]
    public async Task Update_既定を切り替えると以前の既定は外れる()
    {
        var owner = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        var inProgress = await Data.GetStatusAsync(project, StatusCategory.InProgress);

        await Host.RunAsync<WorkflowStatusService, WorkflowStatusResponse>(owner, s => s.UpdateAsync(
            project.Key, inProgress.Id, new UpdateWorkflowStatusRequest { IsDefault = true }, Ct));

        var statuses = await Data.GetStatusesAsync(project);
        Assert.Equal(inProgress.Id, Assert.Single(statuses, s => s.IsDefault).Id);
    }

    [Fact]
    public async Task Reorder_全件の並びを指定する()
    {
        var owner = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        var ids = (await Data.GetStatusesAsync(project)).Select(s => s.Id).Reverse().ToList();

        var result = await Host.RunAsync<WorkflowStatusService, IReadOnlyList<WorkflowStatusResponse>>(owner, s => s.ReorderAsync(
            project.Key, new ReorderWorkflowStatusesRequest { StatusIds = ids }, Ct));

        Assert.Equal(ids, result.Select(s => s.Id).ToList());
        await AppAssert.ValidationAsync("statusIds", () => Host.RunAsync<WorkflowStatusService, IReadOnlyList<WorkflowStatusResponse>>(owner, s => s.ReorderAsync(
            project.Key, new ReorderWorkflowStatusesRequest { StatusIds = ids.Take(3).ToList() }, Ct)));
    }

    [Fact]
    public async Task 編集者はステータスを管理できない()
    {
        var owner = await Data.CreateUserAsync();
        var editor = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        await Data.AddMemberAsync(project, editor, ProjectRole.Editor);

        await AppAssert.ForbiddenAsync(() => Host.RunAsync<WorkflowStatusService, WorkflowStatusResponse>(editor, s => s.CreateAsync(project.Key,
            new CreateWorkflowStatusRequest { Name = "保留", Category = StatusCategory.InProgress, Color = StatusColor.Yellow }, Ct)));
    }
}
