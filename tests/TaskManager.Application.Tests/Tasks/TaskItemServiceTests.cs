using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Common;
using TaskManager.Application.Tasks;
using TaskManager.Domain.Common;
using TaskManager.Testing;

namespace TaskManager.Application.Tests.Tasks;

public class TaskItemServiceTests(DatabaseFixture fixture) : ApplicationTestBase(fixture)
{
    private async Task<(TestUser Owner, TestProject Project)> NewProjectAsync()
    {
        var owner = await Data.CreateUserAsync();
        return (owner, await Data.CreateProjectAsync(owner));
    }

    private Task<TaskDetailResponse> CreateAsync(TestUser actor, string key, CreateTaskRequest request) =>
        Host.RunAsync<TaskItemService, TaskDetailResponse>(actor, s => s.CreateAsync(key, request, Ct));

    private Task<TaskDetailResponse> UpdateAsync(TestUser actor, string key, int number, UpdateTaskRequest request) =>
        Host.RunAsync<TaskItemService, TaskDetailResponse>(actor, s => s.UpdateAsync(key, number, request, Ct));

    private Task<PagedResponse<TaskSummaryResponse>> ListAsync(TestUser actor, string key, TaskListQuery query) =>
        Host.RunAsync<TaskItemService, PagedResponse<TaskSummaryResponse>>(actor, s => s.ListAsync(key, query, Ct));

    [Fact]
    public async Task Create_タイトルだけで既定値のタスクを作り番号を順に振る()
    {
        var (owner, project) = await NewProjectAsync();

        var first = await CreateAsync(owner, project.Key, new CreateTaskRequest { Title = "見積書の作成" });
        var second = await CreateAsync(owner, project.Key, new CreateTaskRequest { Title = "議事録の共有" });

        Assert.Equal((1, $"{project.Key}-1"), (first.Number, first.Key));
        Assert.Equal(2, second.Number);
        Assert.Equal("未着手", first.Status.Name);
        Assert.Equal(TaskPriority.Normal, first.Priority);
        Assert.Null(first.Assignee);
        Assert.Equal(owner.Id, first.CreatedBy?.Id);
        Assert.True(first.Permissions.CanEdit && first.Permissions.CanDelete);
        var taskId = await Data.QueryAsync(db => db.TaskItems.Where(t => t.ProjectId == project.Id && t.Number == 1).Select(t => t.Id).SingleAsync(Ct));
        var history = await Data.QueryAsync(db => db.TaskHistoryEntries.Where(h => h.TaskId == taskId).ToListAsync(Ct));
        Assert.Equal(TaskHistoryKind.Created, Assert.Single(history).Kind);
    }

    [Fact]
    public async Task Create_担当者を指定すると本人にお知らせする()
    {
        var (owner, project) = await NewProjectAsync();
        var editor = await Data.CreateUserAsync();
        await Data.AddMemberAsync(project, editor, ProjectRole.Editor);

        var created = await CreateAsync(owner, project.Key, new CreateTaskRequest
        {
            Title = "会場の候補を3つ挙げる",
            AssigneeUserId = editor.Id,
            DueDate = new DateOnly(2026, 10, 9),
            Priority = TaskPriority.Urgent,
        });

        Assert.Equal(editor.Id, created.Assignee?.Id);
        Assert.Equal(new DateOnly(2026, 10, 9), created.DueDate);
        var notice = await Data.QueryAsync(db => db.Notices.SingleAsync(n => n.RecipientUserId == editor.Id, Ct));
        Assert.Equal(NoticeKind.TaskAssigned, notice.Kind);
        Assert.Equal($"/projects/{project.Key}/tasks/{created.Number}", notice.LinkPath);
    }

    [Fact]
    public async Task Create_自分を担当者にしてもお知らせは作らない()
    {
        var (owner, project) = await NewProjectAsync();
        await CreateAsync(owner, project.Key, new CreateTaskRequest { Title = "自分の作業", AssigneeUserId = owner.Id });
        Assert.False(await Data.QueryAsync(db => db.Notices.AnyAsync(n => n.RecipientUserId == owner.Id, Ct)));
    }

    [Fact]
    public async Task Create_閲覧者_非メンバー_停止中の利用者は担当者にできない()
    {
        var (owner, project) = await NewProjectAsync();
        var viewer = await Data.CreateUserAsync();
        var outsider = await Data.CreateUserAsync();
        await Data.AddMemberAsync(project, viewer, ProjectRole.Viewer);

        foreach (var id in new[] { viewer.Id, outsider.Id, Guid.NewGuid() })
        {
            await AppAssert.ValidationAsync("assigneeUserId",
                () => CreateAsync(owner, project.Key, new CreateTaskRequest { Title = "担当者の検証", AssigneeUserId = id }));
        }
    }

    [Fact]
    public async Task Create_別プロジェクトのステータス_タグ_マイルストーン_親は指定できない()
    {
        var (owner, project) = await NewProjectAsync();
        var other = await Data.CreateProjectAsync(owner);
        var foreignStatus = await Data.GetStatusAsync(other, StatusCategory.InProgress);
        var foreignTag = await Data.CreateTagAsync(other);
        var foreignMilestone = await Data.CreateMilestoneAsync(other);
        var foreignTask = await Data.CreateTaskAsync(other, owner);

        await AppAssert.ValidationAsync("statusId", () => CreateAsync(owner, project.Key, new CreateTaskRequest { Title = "IDOR", StatusId = foreignStatus.Id }));
        await AppAssert.ValidationAsync("tagIds", () => CreateAsync(owner, project.Key, new CreateTaskRequest { Title = "IDOR", TagIds = [foreignTag.Id] }));
        await AppAssert.ValidationAsync("milestoneId", () => CreateAsync(owner, project.Key, new CreateTaskRequest { Title = "IDOR", MilestoneId = foreignMilestone.Id }));
        // 番号は URL のプロジェクト内で解決するため、他プロジェクトにしかない番号は「見つからない」
        await AppAssert.ValidationAsync("parentTaskNumber", () => CreateAsync(owner, project.Key, new CreateTaskRequest { Title = "IDOR", ParentTaskNumber = foreignTask.Number }));
        Assert.False(await Data.QueryAsync(db => db.TaskItems.AnyAsync(t => t.ProjectId == project.Id, Ct)));
    }

    [Fact]
    public async Task Create_重複したタグと終了したマイルストーンを拒否する()
    {
        var (owner, project) = await NewProjectAsync();
        var tag = await Data.CreateTagAsync(project);
        var closed = await Data.CreateMilestoneAsync(project, closed: true);

        await AppAssert.ValidationAsync("tagIds", () => CreateAsync(owner, project.Key, new CreateTaskRequest { Title = "重複", TagIds = [tag.Id, tag.Id] }));
        await AppAssert.ThrowsAsync(AppErrorKind.Conflict, ErrorCodes.MilestoneClosed,
            () => CreateAsync(owner, project.Key, new CreateTaskRequest { Title = "終了済み", MilestoneId = closed.Id }));
    }

    [Fact]
    public async Task Create_サブタスクの下にはサブタスクを作れない()
    {
        var (owner, project) = await NewProjectAsync();
        var parent = await CreateAsync(owner, project.Key, new CreateTaskRequest { Title = "親" });
        var child = await CreateAsync(owner, project.Key, new CreateTaskRequest { Title = "子", ParentTaskNumber = parent.Number });
        Assert.Equal(parent.Key, child.Parent?.Key);

        await AppAssert.ValidationAsync("parentTaskNumber",
            () => CreateAsync(owner, project.Key, new CreateTaskRequest { Title = "孫", ParentTaskNumber = child.Number }));
    }

    [Fact]
    public async Task Create_期限が開始日より前なら項目エラー()
    {
        var (owner, project) = await NewProjectAsync();
        await AppAssert.ValidationAsync("dueDate", () => CreateAsync(owner, project.Key, new CreateTaskRequest
        {
            Title = "日付", StartDate = new DateOnly(2026, 10, 10), DueDate = new DateOnly(2026, 10, 9),
        }));
    }

    [Fact]
    public async Task Create_閲覧者はタスクを作れない()
    {
        var (owner, project) = await NewProjectAsync();
        var viewer = await Data.CreateUserAsync();
        await Data.AddMemberAsync(project, viewer, ProjectRole.Viewer);

        await AppAssert.ForbiddenAsync(() => CreateAsync(viewer, project.Key, new CreateTaskRequest { Title = "閲覧者" }));
        var task = await Data.CreateTaskAsync(project, owner);
        var detail = await Host.RunAsync<TaskItemService, TaskDetailResponse>(viewer, s => s.GetAsync(project.Key, task.Number, Ct));
        Assert.Equal(new TaskPermissions(false, false, false, false), detail.Permissions);
    }

    [Fact]
    public async Task Create_20件を並行して作成しても番号は重複も欠番もない()
    {
        var (owner, project) = await NewProjectAsync();

        var created = await Task.WhenAll(Enumerable.Range(1, 20).Select(i =>
            CreateAsync(owner, project.Key, new CreateTaskRequest { Title = $"並行作成 {i}" })));

        Assert.Equal(Enumerable.Range(1, 20), created.Select(t => t.Number).Order());
    }

    [Fact]
    public async Task Update_古い版は409で他の人の変更を上書きしない()
    {
        var (owner, project) = await NewProjectAsync();
        var task = await CreateAsync(owner, project.Key, new CreateTaskRequest { Title = "元のタイトル" });

        var updated = await UpdateAsync(owner, project.Key, task.Number, new UpdateTaskRequest { Title = "最初の更新", Version = task.Version });
        Assert.Equal(task.Version + 1, updated.Version);

        await AppAssert.ThrowsAsync(AppErrorKind.Conflict, ErrorCodes.ConcurrencyConflict,
            () => UpdateAsync(owner, project.Key, task.Number, new UpdateTaskRequest { Title = "古い版での更新", Version = task.Version }));
        var current = await Host.RunAsync<TaskItemService, TaskDetailResponse>(owner, s => s.GetAsync(project.Key, task.Number, Ct));
        Assert.Equal("最初の更新", current.Title);
    }

    [Fact]
    public async Task Update_完了にすると完了日時を設定し作成者にお知らせし未着手に戻すと消す()
    {
        var (owner, project) = await NewProjectAsync();
        var editor = await Data.CreateUserAsync();
        await Data.AddMemberAsync(project, editor, ProjectRole.Editor);
        var task = await CreateAsync(owner, project.Key, new CreateTaskRequest { Title = "完了テスト" });
        var done = await Data.GetStatusAsync(project, StatusCategory.Done);
        var notStarted = await Data.GetStatusAsync(project, StatusCategory.NotStarted);

        var completed = await UpdateAsync(editor, project.Key, task.Number, new UpdateTaskRequest { StatusId = done.Id, Version = task.Version });
        Assert.Equal(Host.Time.GetUtcNow().UtcDateTime, completed.CompletedAtUtc);
        var notice = await Data.QueryAsync(db => db.Notices.SingleAsync(n => n.RecipientUserId == owner.Id && n.Kind == NoticeKind.TaskCompleted, Ct));
        Assert.Contains("完了しました", notice.Message, StringComparison.Ordinal);

        var reopened = await UpdateAsync(editor, project.Key, task.Number, new UpdateTaskRequest { StatusId = notStarted.Id, Version = completed.Version });
        Assert.Null(reopened.CompletedAtUtc);
    }

    [Fact]
    public async Task Update_変更した項目ごとに履歴を残し変わらない項目は残さない()
    {
        var (owner, project) = await NewProjectAsync();
        var task = await CreateAsync(owner, project.Key, new CreateTaskRequest { Title = "履歴テスト", Priority = TaskPriority.Normal });

        await UpdateAsync(owner, project.Key, task.Number, new UpdateTaskRequest
        {
            Title = "履歴テスト",
            Priority = TaskPriority.High,
            DueDate = new DateOnly(2026, 10, 31),
            Description = "説明を追加",
            Version = task.Version,
        });

        var history = await Host.RunAsync<TaskHistoryService, PagedResponse<TaskHistoryEntryResponse>>(
            owner, s => s.ListAsync(project.Key, task.Number, null, null, Ct));
        var kinds = history.Items.Select(h => h.Kind).ToHashSet();
        Assert.Contains(TaskHistoryKind.PriorityChanged, kinds);
        Assert.Contains(TaskHistoryKind.DueDateChanged, kinds);
        Assert.Contains(TaskHistoryKind.DescriptionChanged, kinds);
        Assert.DoesNotContain(TaskHistoryKind.TitleChanged, kinds);
        Assert.DoesNotContain(TaskHistoryKind.StartDateChanged, kinds);
        var priority = history.Items.Single(h => h.Kind == TaskHistoryKind.PriorityChanged);
        Assert.Equal(("中", "高"), (priority.OldValue, priority.NewValue));
    }

    [Fact]
    public async Task Update_nullを送ると値を消し送らない項目は変えない()
    {
        var (owner, project) = await NewProjectAsync();
        var task = await CreateAsync(owner, project.Key, new CreateTaskRequest
        {
            Title = "部分更新", AssigneeUserId = owner.Id, DueDate = new DateOnly(2026, 10, 20), Description = "説明",
        });

        var updated = await UpdateAsync(owner, project.Key, task.Number, new UpdateTaskRequest { DueDate = null, Version = task.Version });

        Assert.Null(updated.DueDate);
        Assert.Equal(owner.Id, updated.Assignee?.Id);
        Assert.Equal("説明", updated.Description);
    }

    [Fact]
    public async Task Update_ステータスにnullは指定できない()
    {
        var (owner, project) = await NewProjectAsync();
        var task = await CreateAsync(owner, project.Key, new CreateTaskRequest { Title = "ステータス" });
        await AppAssert.ValidationAsync("statusId",
            () => UpdateAsync(owner, project.Key, task.Number, new UpdateTaskRequest { StatusId = null, Version = task.Version }));
    }

    [Fact]
    public async Task Update_サブタスクを持つタスクは子にできず自分自身も親にできない()
    {
        var (owner, project) = await NewProjectAsync();
        var a = await CreateAsync(owner, project.Key, new CreateTaskRequest { Title = "A" });
        var b = await CreateAsync(owner, project.Key, new CreateTaskRequest { Title = "B" });
        await CreateAsync(owner, project.Key, new CreateTaskRequest { Title = "Aの子", ParentTaskNumber = a.Number });

        await AppAssert.ValidationAsync("parentTaskNumber",
            () => UpdateAsync(owner, project.Key, a.Number, new UpdateTaskRequest { ParentTaskNumber = b.Number, Version = a.Version }));
        await AppAssert.ValidationAsync("parentTaskNumber",
            () => UpdateAsync(owner, project.Key, b.Number, new UpdateTaskRequest { ParentTaskNumber = b.Number, Version = b.Version }));
    }

    [Fact]
    public async Task Delete_編集者は自分が作ったタスクだけ削除でき管理者は誰のものでも削除できる()
    {
        var (owner, project) = await NewProjectAsync();
        var editor = await Data.CreateUserAsync();
        await Data.AddMemberAsync(project, editor, ProjectRole.Editor);
        var ownersTask = await CreateAsync(owner, project.Key, new CreateTaskRequest { Title = "管理者のタスク" });
        var editorsTask = await CreateAsync(editor, project.Key, new CreateTaskRequest { Title = "編集者のタスク" });
        var asEditor = await Host.RunAsync<TaskItemService, TaskDetailResponse>(editor, s => s.GetAsync(project.Key, ownersTask.Number, Ct));
        Assert.False(asEditor.Permissions.CanDelete);
        Assert.True(asEditor.Permissions.CanEdit);

        await AppAssert.ForbiddenAsync(() => Host.RunAsync<TaskItemService>(editor, s => s.DeleteAsync(project.Key, ownersTask.Number, false, Ct)));
        await Host.RunAsync<TaskItemService>(editor, s => s.DeleteAsync(project.Key, editorsTask.Number, false, Ct));
        await Host.RunAsync<TaskItemService>(owner, s => s.DeleteAsync(project.Key, ownersTask.Number, false, Ct));

        Assert.False(await Data.QueryAsync(db => db.TaskItems.AnyAsync(t => t.ProjectId == project.Id, Ct)));
    }

    [Fact]
    public async Task Delete_サブタスクがあれば409でまとめて削除を指定すれば消す()
    {
        var (owner, project) = await NewProjectAsync();
        var parent = await CreateAsync(owner, project.Key, new CreateTaskRequest { Title = "親", AssigneeUserId = owner.Id });
        await CreateAsync(owner, project.Key, new CreateTaskRequest { Title = "子", ParentTaskNumber = parent.Number });

        await AppAssert.ThrowsAsync(AppErrorKind.Conflict, ErrorCodes.TaskHasSubtasks,
            () => Host.RunAsync<TaskItemService>(owner, s => s.DeleteAsync(project.Key, parent.Number, false, Ct)));
        await Host.RunAsync<TaskItemService>(owner, s => s.DeleteAsync(project.Key, parent.Number, true, Ct));

        Assert.False(await Data.QueryAsync(db => db.TaskItems.AnyAsync(t => t.ProjectId == project.Id, Ct)));
    }

    [Fact]
    public async Task Get_他プロジェクトのタスク番号はURLのプロジェクトでは見つからない()
    {
        var (owner, project) = await NewProjectAsync();
        var other = await Data.CreateProjectAsync(await Data.CreateUserAsync());
        await Data.CreateTaskAsync(other, owner);

        await AppAssert.NotFoundAsync(() => Host.RunAsync<TaskItemService, TaskDetailResponse>(owner, s => s.GetAsync(project.Key, 1, Ct)));
        await AppAssert.NotFoundAsync(() => Host.RunAsync<TaskItemService, TaskDetailResponse>(owner, s => s.GetAsync(other.Key, 1, Ct)));
    }

    [Fact]
    public async Task List_既定は未完了だけで完了も含める指定ができる()
    {
        var (owner, project) = await NewProjectAsync();
        await Data.CreateTaskAsync(project, owner, "未着手のタスク");
        await Data.CreateTaskAsync(project, owner, "完了したタスク", category: StatusCategory.Done);

        var open = await ListAsync(owner, project.Key, new TaskListQuery());
        var all = await ListAsync(owner, project.Key, new TaskListQuery { IncludeClosed = true });

        Assert.Equal(["未着手のタスク"], open.Items.Select(t => t.Title).ToArray());
        Assert.Equal(2, all.TotalCount);
    }

    [Theory]
    [InlineData("%", 1)]
    [InlineData("_", 1)]
    [InlineData("' OR 1=1 --", 0)]
    [InlineData("100%", 1)]
    public async Task List_タイトル検索の記号は文字として扱う(string q, int expected)
    {
        var (owner, project) = await NewProjectAsync();
        await Data.CreateTaskAsync(project, owner, "進捗100%の確認");
        await Data.CreateTaskAsync(project, owner, "file_name の整理");
        await Data.CreateTaskAsync(project, owner, "通常のタスク");

        var result = await ListAsync(owner, project.Key, new TaskListQuery { Q = q });

        Assert.Equal(expected, result.TotalCount);
    }

    [Fact]
    public async Task List_期限切れの絞り込みは利用者の今日を基準にする()
    {
        var (owner, project) = await NewProjectAsync();
        var today = new DateOnly(2026, 10, 8);
        await Data.CreateTaskAsync(project, owner, "昨日まで", dueDate: today.AddDays(-1));
        await Data.CreateTaskAsync(project, owner, "今日まで", dueDate: today);
        await Data.CreateTaskAsync(project, owner, "完了済みの期限切れ", dueDate: today.AddDays(-3), category: StatusCategory.Done);

        var result = await ListAsync(owner, project.Key, new TaskListQuery { Overdue = true, IncludeClosed = true });

        var item = Assert.Single(result.Items);
        Assert.Equal("昨日まで", item.Title);
        Assert.True(item.IsOverdue);
    }

    [Theory]
    [InlineData("-updatedAt")]
    [InlineData("number")]
    [InlineData("-number")]
    [InlineData("dueDate")]
    [InlineData("-dueDate")]
    [InlineData("priority")]
    [InlineData("-priority")]
    [InlineData("status")]
    [InlineData("title")]
    [InlineData("assignee")]
    [InlineData("-createdAt")]
    public async Task List_許可された並べ替えはすべてSQLに変換できる(string sort)
    {
        var (owner, project) = await NewProjectAsync();
        await Data.CreateTaskAsync(project, owner, "A", assigneeUserId: owner.Id, dueDate: new DateOnly(2026, 10, 9));
        await Data.CreateTaskAsync(project, owner, "B");

        var result = await ListAsync(owner, project.Key, new TaskListQuery { Sort = sort });

        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task List_許可されていない並べ替えは400()
    {
        var (owner, project) = await NewProjectAsync();
        await AppAssert.ThrowsAsync(AppErrorKind.BadRequest, ErrorCodes.InvalidSort,
            () => ListAsync(owner, project.Key, new TaskListQuery { Sort = "password" }));
    }

    [Fact]
    public async Task List_担当者と担当者なしの絞り込み()
    {
        var (owner, project) = await NewProjectAsync();
        await Data.CreateTaskAsync(project, owner, "自分", assigneeUserId: owner.Id);
        await Data.CreateTaskAsync(project, owner, "担当なし");

        Assert.Equal("自分", Assert.Single((await ListAsync(owner, project.Key, new TaskListQuery { AssignedToMe = true })).Items).Title);
        Assert.Equal("担当なし", Assert.Single((await ListAsync(owner, project.Key, new TaskListQuery { Unassigned = true })).Items).Title);
        Assert.Equal(2, (await ListAsync(owner, project.Key, new TaskListQuery { AssignedToMe = true, Unassigned = true })).TotalCount);
    }

    [Fact]
    public async Task List_ページングは100件まで()
    {
        var (owner, project) = await NewProjectAsync();
        await Data.CreateTaskAsync(project, owner);
        var result = await ListAsync(owner, project.Key, new TaskListQuery { Page = 1, PageSize = 1000 });
        Assert.Equal(100, result.PageSize);
    }
}
