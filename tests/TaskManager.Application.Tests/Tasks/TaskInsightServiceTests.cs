using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Common;
using TaskManager.Application.Tasks;
using TaskManager.Application.Users;
using TaskManager.Domain.Common;
using TaskManager.Testing;

namespace TaskManager.Application.Tests.Tasks;

/// <summary>ガントチャートの日程（REQ-GNT-01。MySQL での期間検索）。既定の現在時刻は 2026-10-08（木）10:00 JST。</summary>
public class TaskScheduleServiceTests(DatabaseFixture fixture) : ApplicationTestBase(fixture)
{
    private static DateOnly D(string value) => DateOnly.ParseExact(value, "yyyy-MM-dd");

    private Task<TaskScheduleResponse> GetAsync(TestUser actor, string key, string from, string to, bool? includeClosed = null) =>
        Host.RunAsync<TaskScheduleService, TaskScheduleResponse>(actor, s => s.GetAsync(key,
            new TaskScheduleQuery { From = D(from), To = D(to), IncludeClosed = includeClosed }, Ct));

    [Fact]
    public async Task Get_期間と重なるタスクだけを棒の始点の順に返し日付未設定は別にする()
    {
        var owner = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        await Data.CreateTaskAsync(project, owner, "期間より前", startDate: D("2026-09-20"), dueDate: D("2026-10-04"));
        await Data.CreateTaskAsync(project, owner, "初日に終わる", startDate: D("2026-09-28"), dueDate: D("2026-10-05"));
        await Data.CreateTaskAsync(project, owner, "期間を覆う", startDate: D("2026-09-01"), dueDate: D("2026-12-31"));
        await Data.CreateTaskAsync(project, owner, "期限だけ", dueDate: D("2026-10-10"));
        await Data.CreateTaskAsync(project, owner, "開始日だけ", startDate: D("2026-10-18"));
        await Data.CreateTaskAsync(project, owner, "期間より後", startDate: D("2026-10-19"), dueDate: D("2026-10-20"));
        await Data.CreateTaskAsync(project, owner, "日付なし1");
        await Data.CreateTaskAsync(project, owner, "日付なし2");

        var result = await GetAsync(owner, project.Key, "2026-10-05", "2026-10-18");

        Assert.Equal(["期間を覆う", "初日に終わる", "期限だけ", "開始日だけ"], result.Items.Select(i => i.Title));
        Assert.False(result.Truncated);
        Assert.Equal(2, result.UndatedCount);
        Assert.Equal(["日付なし1", "日付なし2"], result.UndatedItems.Select(i => i.Title).Order());
        Assert.Equal((D("2026-10-05"), D("2026-10-18"), D("2026-10-08"), "Asia/Tokyo"), (result.From, result.To, result.Today, result.TimeZoneId));
        var dueOnly = result.Items.Single(i => i.Title == "期限だけ");
        Assert.Null(dueOnly.StartDate);
        Assert.Equal(D("2026-10-10"), dueOnly.DueDate);
    }

    [Fact]
    public async Task Get_既定は未完了のみで完了と中止はincludeClosedで含める()
    {
        var owner = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        await Data.CreateTaskAsync(project, owner, "対応中", dueDate: D("2026-10-09"), category: StatusCategory.InProgress);
        await Data.CreateTaskAsync(project, owner, "完了", dueDate: D("2026-10-09"), category: StatusCategory.Done);
        await Data.CreateTaskAsync(project, owner, "中止", dueDate: D("2026-10-09"), category: StatusCategory.Canceled);
        await Data.CreateTaskAsync(project, owner, "日付なしの完了", category: StatusCategory.Done);

        var open = await GetAsync(owner, project.Key, "2026-10-01", "2026-10-31");
        var all = await GetAsync(owner, project.Key, "2026-10-01", "2026-10-31", includeClosed: true);

        Assert.Equal(["対応中"], open.Items.Select(i => i.Title));
        Assert.Equal(0, open.UndatedCount);
        // 棒の始点が同じなら番号順
        Assert.Equal(["対応中", "完了", "中止"], all.Items.Select(i => i.Title));
        Assert.Equal(1, all.UndatedCount);
    }

    [Fact]
    public async Task Get_期限切れは利用者のタイムゾーンの今日で判定する()
    {
        // 2026-10-08 01:00 UTC は 東京では 10/8、ロサンゼルスでは 10/7
        var owner = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        await Data.CreateTaskAsync(project, owner, "10月7日まで", dueDate: D("2026-10-07"));

        var tokyo = await GetAsync(owner, project.Key, "2026-10-01", "2026-10-14");
        await Host.RunAsync<MyAccountService, CurrentUserResponse>(owner, s => s.UpdateProfileAsync(new UpdateMeRequest { TimeZoneId = "America/Los_Angeles" }, Ct));
        var losAngeles = await GetAsync(owner, project.Key, "2026-10-01", "2026-10-14");

        Assert.True(Assert.Single(tokyo.Items).IsOverdue);
        Assert.Equal(D("2026-10-07"), losAngeles.Today);
        Assert.False(Assert.Single(losAngeles.Items).IsOverdue);
    }

    [Fact]
    public async Task Get_他のプロジェクトのタスクを含めない()
    {
        var owner = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        var other = await Data.CreateProjectAsync(owner);
        await Data.CreateTaskAsync(project, owner, "自分のプロジェクト", dueDate: D("2026-10-09"));
        await Data.CreateTaskAsync(other, owner, "別のプロジェクト", dueDate: D("2026-10-09"));
        await Data.CreateTaskAsync(other, owner, "別のプロジェクトの日付なし");

        var result = await GetAsync(owner, project.Key, "2026-10-01", "2026-10-31");

        Assert.Equal("自分のプロジェクト", Assert.Single(result.Items).Title);
        Assert.Equal(0, result.UndatedCount);
    }

    [Fact]
    public async Task Get_非メンバーは404で閲覧者とアーカイブ中は参照できる()
    {
        var owner = await Data.CreateUserAsync();
        var viewer = await Data.CreateUserAsync();
        var stranger = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        await Data.AddMemberAsync(project, viewer, ProjectRole.Viewer);
        await Data.CreateTaskAsync(project, owner, "日程あり", dueDate: D("2026-10-09"));
        await Data.ArchiveProjectAsync(project);

        await AppAssert.NotFoundAsync(() => GetAsync(stranger, project.Key, "2026-10-01", "2026-10-31"));
        Assert.Single((await GetAsync(viewer, project.Key, "2026-10-01", "2026-10-31")).Items);
    }

    [Theory]
    [InlineData("2026-10-08", "2026-10-07", "to")]
    [InlineData("2026-01-01", "2027-01-02", "to")]
    [InlineData("1999-12-31", "2000-01-31", "from")]
    public async Task Get_期間が不正なら項目エラー(string from, string to, string field)
    {
        var owner = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);

        await AppAssert.ValidationAsync(field, () => GetAsync(owner, project.Key, from, to));
    }

    [Fact]
    public async Task Get_期間の指定がなければ項目エラー()
    {
        var owner = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);

        await AppAssert.ValidationAsync("from", () => Host.RunAsync<TaskScheduleService, TaskScheduleResponse>(owner,
            s => s.GetAsync(project.Key, new TaskScheduleQuery { To = D("2026-10-31") }, Ct)));
        await AppAssert.ValidationAsync("to", () => Host.RunAsync<TaskScheduleService, TaskScheduleResponse>(owner,
            s => s.GetAsync(project.Key, new TaskScheduleQuery { From = D("2026-10-01") }, Ct)));
    }
}

/// <summary>ステータス別の件数・割合（REQ-CHT-01・02。MySQL での GROUP BY 集計）。</summary>
public class TaskStatusSummaryServiceTests(DatabaseFixture fixture) : ApplicationTestBase(fixture)
{
    private Task<TaskStatusSummaryResponse> ForProjectAsync(TestUser actor, string key) =>
        Host.RunAsync<TaskStatusSummaryService, TaskStatusSummaryResponse>(actor, s => s.GetForProjectAsync(key, Ct));

    private Task<TaskStatusSummaryResponse> ForMeAsync(TestUser actor, string? key = null) =>
        Host.RunAsync<TaskStatusSummaryService, TaskStatusSummaryResponse>(actor, s => s.GetForMeAsync(key, Ct));

    [Fact]
    public async Task Project_全ステータスを並び順で0件も含めて件数と割合を返す()
    {
        var owner = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        for (var i = 0; i < 5; i++)
        {
            await Data.CreateTaskAsync(project, owner, $"未着手{i}");
        }

        for (var i = 0; i < 4; i++)
        {
            await Data.CreateTaskAsync(project, owner, $"対応中{i}", category: StatusCategory.InProgress);
        }

        for (var i = 0; i < 3; i++)
        {
            await Data.CreateTaskAsync(project, owner, $"完了{i}", category: StatusCategory.Done);
        }

        var result = await ForProjectAsync(owner, project.Key);

        Assert.Equal(StatusSummaryGrouping.Status, result.GroupedBy);
        Assert.Equal(12, result.TotalCount);
        Assert.Equal(["未着手", "対応中", "確認待ち", "完了", "中止"], result.Items.Select(i => i.Name));
        Assert.Equal([5, 4, 0, 3, 0], result.Items.Select(i => i.Count));
        Assert.Equal([41.7m, 33.3m, 0m, 25.0m, 0m], result.Items.Select(i => i.Percent));
        Assert.Equal(100.0m, result.Items.Sum(i => i.Percent));
        Assert.Equal([1, 2, 3, 4, 5], result.Items.Select(i => i.SortOrder));
        Assert.Equal(StatusColor.Purple, result.Items[2].Color);
        var statuses = await Data.GetStatusesAsync(project);
        Assert.Equal(statuses.Select(s => (Guid?)s.Id), result.Items.Select(i => i.StatusId));
    }

    [Fact]
    public async Task Project_ステータスの並べ替えと追加を反映し他のプロジェクトを含めない()
    {
        var owner = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        var other = await Data.CreateProjectAsync(owner);
        var extra = await Data.CreateStatusAsync(project, "保留");
        var task = await Data.CreateTaskAsync(project, owner, "保留のタスク");
        await Data.CreateTaskAsync(other, owner, "別のプロジェクトのタスク");
        await Data.QueryAsync(async db =>
        {
            var s = await db.WorkflowStatuses.SingleAsync(x => x.Id == extra.Id, Ct);
            s.MoveTo(0);
            var t = await db.TaskItems.SingleAsync(x => x.Id == task.Id, Ct);
            t.ChangeStatus(s, StatusCategory.NotStarted, DateTime.UtcNow);
            await db.SaveChangesAsync(Ct);
            return true;
        });

        var result = await ForProjectAsync(owner, project.Key);

        Assert.Equal(6, result.Items.Count);
        Assert.Equal("保留", result.Items[0].Name);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal((1, 100.0m), (result.Items[0].Count, result.Items[0].Percent));
        Assert.All(result.Items.Skip(1), i => Assert.Equal(0, i.Count));
    }

    [Fact]
    public async Task Project_タスクがなければ合計0で全て0件()
    {
        var owner = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);

        var result = await ForProjectAsync(owner, project.Key);

        Assert.Equal(0, result.TotalCount);
        Assert.Equal(5, result.Items.Count);
        Assert.All(result.Items, i => Assert.Equal((0, 0m), (i.Count, i.Percent)));
    }

    [Fact]
    public async Task Project_非メンバーは404でアーカイブ中も閲覧できる()
    {
        var owner = await Data.CreateUserAsync();
        var stranger = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        await Data.CreateTaskAsync(project, owner);
        await Data.ArchiveProjectAsync(project);

        await AppAssert.NotFoundAsync(() => ForProjectAsync(stranger, project.Key));
        Assert.Equal(1, (await ForProjectAsync(owner, project.Key)).TotalCount);
    }

    [Fact]
    public async Task Me_全プロジェクトは区分別で他人の担当とアーカイブ中と非参加のプロジェクトを含めない()
    {
        var me = await Data.CreateUserAsync();
        var other = await Data.CreateUserAsync();
        var a = await Data.CreateProjectAsync(me);
        var b = await Data.CreateProjectAsync(other);
        await Data.AddMemberAsync(b, me, ProjectRole.Editor);
        var archived = await Data.CreateProjectAsync(me);
        var notMember = await Data.CreateProjectAsync(other);
        await Data.AddMemberAsync(a, other, ProjectRole.Editor);
        await Data.CreateTaskAsync(a, me, "A 未着手", me.Id);
        await Data.CreateTaskAsync(a, me, "A 完了", me.Id, category: StatusCategory.Done);
        await Data.CreateTaskAsync(b, other, "B 対応中", me.Id, category: StatusCategory.InProgress);
        await Data.CreateTaskAsync(b, other, "B 確認待ちは区分が進行中", me.Id, category: StatusCategory.InProgress);
        await Data.CreateTaskAsync(a, me, "他人の担当", other.Id);
        await Data.CreateTaskAsync(archived, me, "アーカイブ中", me.Id);
        await Data.ArchiveProjectAsync(archived);
        await Data.CreateTaskAsync(notMember, other, "参加していない", me.Id);

        var result = await ForMeAsync(me);

        Assert.Equal(StatusSummaryGrouping.Category, result.GroupedBy);
        Assert.Equal(4, result.TotalCount);
        Assert.Equal(["未着手", "進行中", "完了", "中止"], result.Items.Select(i => i.Name));
        Assert.Equal([1, 2, 1, 0], result.Items.Select(i => i.Count));
        Assert.Equal([25.0m, 50.0m, 25.0m, 0m], result.Items.Select(i => i.Percent));
        Assert.Equal([StatusCategory.NotStarted, StatusCategory.InProgress, StatusCategory.Done, StatusCategory.Canceled], result.Items.Select(i => i.Category));
        Assert.Equal([StatusColor.Gray, StatusColor.Blue, StatusColor.Green, StatusColor.Red], result.Items.Select(i => i.Color));
        Assert.All(result.Items, i => Assert.Null(i.StatusId));
    }

    [Fact]
    public async Task Me_プロジェクトを指定するとそのプロジェクトのステータス別に自分の担当だけを集計する()
    {
        var me = await Data.CreateUserAsync();
        var other = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(me);
        await Data.AddMemberAsync(project, other, ProjectRole.Editor);
        await Data.CreateTaskAsync(project, me, "自分の未着手", me.Id);
        await Data.CreateTaskAsync(project, me, "自分の対応中", me.Id, category: StatusCategory.InProgress);
        await Data.CreateTaskAsync(project, me, "他人の担当", other.Id);
        await Data.CreateTaskAsync(project, me, "担当なし");

        var result = await ForMeAsync(me, project.Key.ToLowerInvariant());

        Assert.Equal(StatusSummaryGrouping.Status, result.GroupedBy);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal([1, 1, 0, 0, 0], result.Items.Select(i => i.Count));
        Assert.Equal([50.0m, 50.0m, 0m, 0m, 0m], result.Items.Select(i => i.Percent));
    }

    [Fact]
    public async Task Me_参加していないプロジェクトを指定すると404()
    {
        var me = await Data.CreateUserAsync();
        var other = await Data.CreateUserAsync();
        var hidden = await Data.CreateProjectAsync(other);
        await Data.CreateTaskAsync(hidden, other, "他人のプロジェクトで自分が担当", me.Id);

        await AppAssert.NotFoundAsync(() => ForMeAsync(me, hidden.Key));
        await AppAssert.NotFoundAsync(() => ForMeAsync(me, "ZZNOTEXIST"));
    }

    [Fact]
    public async Task Me_担当がなければ合計0()
    {
        var me = await Data.CreateUserAsync();

        var result = await ForMeAsync(me);

        Assert.Equal(0, result.TotalCount);
        Assert.Equal(4, result.Items.Count);
    }
}
