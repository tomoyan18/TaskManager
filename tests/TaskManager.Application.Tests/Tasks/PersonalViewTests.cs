using TaskManager.Application.Common;
using TaskManager.Application.Notices;
using TaskManager.Application.Tasks;
using TaskManager.Application.Users;
using TaskManager.Domain.Common;
using TaskManager.Testing;

namespace TaskManager.Application.Tests.Tasks;

public class MyTasksServiceTests(DatabaseFixture fixture) : ApplicationTestBase(fixture)
{
    [Fact]
    public async Task Get_期限で区分し他人のタスクとアーカイブ中のプロジェクトは含めない()
    {
        // 既定の現在時刻は 2026-10-08（木）10:00 JST
        var me = await Data.CreateUserAsync();
        var other = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(me);
        var archived = await Data.CreateProjectAsync(me);
        await Data.AddMemberAsync(project, other, ProjectRole.Editor);
        await Data.CreateTaskAsync(project, me, "期限切れ", me.Id, new DateOnly(2026, 10, 7));
        await Data.CreateTaskAsync(project, me, "今日", me.Id, new DateOnly(2026, 10, 8));
        await Data.CreateTaskAsync(project, me, "今週", me.Id, new DateOnly(2026, 10, 11));
        await Data.CreateTaskAsync(project, me, "来週以降", me.Id, new DateOnly(2026, 10, 12));
        await Data.CreateTaskAsync(project, me, "期限なし", me.Id);
        await Data.CreateTaskAsync(project, me, "完了済み", me.Id, new DateOnly(2026, 10, 1), StatusCategory.Done);
        await Data.CreateTaskAsync(project, me, "他人の担当", other.Id, new DateOnly(2026, 10, 8));
        await Data.CreateTaskAsync(archived, me, "アーカイブ中", me.Id, new DateOnly(2026, 10, 8));
        await Data.ArchiveProjectAsync(archived);

        var result = await Host.RunAsync<MyTasksService, MyTasksResponse>(me, s => s.GetAsync(true, Ct));

        Assert.Equal(new DateOnly(2026, 10, 8), result.Today);
        Assert.Equal("期限切れ", Assert.Single(result.Buckets.Overdue).Title);
        Assert.Equal("今日", Assert.Single(result.Buckets.Today).Title);
        Assert.Equal("今週", Assert.Single(result.Buckets.ThisWeek).Title);
        Assert.Equal("来週以降", Assert.Single(result.Buckets.Later).Title);
        Assert.Equal("期限なし", Assert.Single(result.Buckets.NoDueDate).Title);
        Assert.Equal("完了済み", Assert.Single(result.RecentlyCompleted).Title);
        Assert.False(result.Truncated);
    }

    [Fact]
    public async Task Get_今日は利用者のタイムゾーンで決める()
    {
        // 2026-10-08 01:00 UTC は 東京では 10/8、ロサンゼルスでは 10/7
        var me = await Data.CreateUserAsync();
        await Host.RunAsync<MyAccountService, CurrentUserResponse>(me, s => s.UpdateProfileAsync(
            new UpdateMeRequest { TimeZoneId = "America/Los_Angeles" }, Ct));
        var project = await Data.CreateProjectAsync(me);
        await Data.CreateTaskAsync(project, me, "10月8日まで", me.Id, new DateOnly(2026, 10, 8));

        var result = await Host.RunAsync<MyTasksService, MyTasksResponse>(me, s => s.GetAsync(false, Ct));

        Assert.Equal(new DateOnly(2026, 10, 7), result.Today);
        Assert.Equal("America/Los_Angeles", result.TimeZoneId);
        Assert.Empty(result.Buckets.Today);
        Assert.Single(result.Buckets.ThisWeek);
    }
}

public class NoticeServiceTests(DatabaseFixture fixture) : ApplicationTestBase(fixture)
{
    [Fact]
    public async Task 自分のお知らせだけを参照し既読にできる()
    {
        var owner = await Data.CreateUserAsync();
        var me = await Data.CreateUserAsync();
        var other = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        await Data.AddMemberAsync(project, me, ProjectRole.Editor);
        await Data.AddMemberAsync(project, other, ProjectRole.Editor);
        await Host.RunAsync<TaskItemService, TaskDetailResponse>(owner, s => s.CreateAsync(project.Key, new CreateTaskRequest { Title = "私の担当", AssigneeUserId = me.Id }, Ct));
        await Host.RunAsync<TaskItemService, TaskDetailResponse>(owner, s => s.CreateAsync(project.Key, new CreateTaskRequest { Title = "他人の担当", AssigneeUserId = other.Id }, Ct));

        var mine = await Host.RunAsync<NoticeService, PagedResponse<NoticeResponse>>(me, s => s.ListAsync(true, null, null, Ct));
        var notice = Assert.Single(mine.Items);
        Assert.Contains("私の担当", notice.Message, StringComparison.Ordinal);
        Assert.Equal(1, (await Host.RunAsync<NoticeService, UnreadNoticeCountResponse>(me, s => s.CountUnreadAsync(Ct))).Count);

        var othersNotice = Assert.Single((await Host.RunAsync<NoticeService, PagedResponse<NoticeResponse>>(other, s => s.ListAsync(true, null, null, Ct))).Items);
        await AppAssert.NotFoundAsync(() => Host.RunAsync<NoticeService>(me, s => s.MarkReadAsync(othersNotice.Id, Ct)));

        await Host.RunAsync<NoticeService>(me, s => s.MarkReadAsync(notice.Id, Ct));
        Assert.Equal(0, (await Host.RunAsync<NoticeService, UnreadNoticeCountResponse>(me, s => s.CountUnreadAsync(Ct))).Count);
        Assert.Equal(1, (await Host.RunAsync<NoticeService, UnreadNoticeCountResponse>(other, s => s.CountUnreadAsync(Ct))).Count);

        await Host.RunAsync<NoticeService>(other, s => s.MarkAllReadAsync(Ct));
        Assert.Equal(0, (await Host.RunAsync<NoticeService, UnreadNoticeCountResponse>(other, s => s.CountUnreadAsync(Ct))).Count);
    }
}

public class TaskSearchServiceTests(DatabaseFixture fixture) : ApplicationTestBase(fixture)
{
    [Fact]
    public async Task 所属するプロジェクトだけを検索しキーで完全一致を先頭にする()
    {
        var me = await Data.CreateUserAsync();
        var stranger = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(me);
        var hidden = await Data.CreateProjectAsync(stranger);
        var word = Unique.Name("検索語");
        var first = await Data.CreateTaskAsync(project, me, "最初のタスク");
        await Data.CreateTaskAsync(project, me, $"{word} を含むタスク");
        await Data.CreateTaskAsync(hidden, stranger, $"{word} を含む非公開タスク");

        var byWord = await Host.RunAsync<TaskSearchService, IReadOnlyList<TaskSearchResultResponse>>(me, s => s.SearchAsync(word, Ct));
        Assert.Equal($"{word} を含むタスク", Assert.Single(byWord).Title);

        var byKey = await Host.RunAsync<TaskSearchService, IReadOnlyList<TaskSearchResultResponse>>(
            me, s => s.SearchAsync($"{project.Key.ToLowerInvariant()}-{first.Number}", Ct));
        Assert.Equal($"{project.Key}-{first.Number}", byKey[0].Key);

        var otherKey = await Host.RunAsync<TaskSearchService, IReadOnlyList<TaskSearchResultResponse>>(
            me, s => s.SearchAsync($"{hidden.Key}-1", Ct));
        Assert.Empty(otherKey);
    }
}
