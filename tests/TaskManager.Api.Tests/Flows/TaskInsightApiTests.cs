using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TaskManager.Api.Tests.Support;
using TaskManager.Domain.Common;
using TaskManager.Testing;

namespace TaskManager.Api.Tests.Flows;

/// <summary>
/// ガントチャート・ステータス別円グラフの API（api-design.md #62〜#64、REQ-GNT-01・02、REQ-CHT-01・02）。
/// 応答は OpenAPI の契約とも照合する。認可の全組み合わせは認可マトリクスで確認する。
/// </summary>
public class TaskInsightApiTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly D(string value) => DateOnly.ParseExact(value, "yyyy-MM-dd");

    private async Task<(HttpClient Client, TestUser Owner, TestProject Project)> NewProjectAsync()
    {
        var owner = await factory.Data.CreateUserAsync();
        var project = await factory.Data.CreateProjectAsync(owner);
        return (await factory.CreateClientAsync(owner), owner, project);
    }

    private static async Task<JsonElement> OkAsync(HttpResponseMessage response, string operationId)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await OpenApiContract.AssertResponseAsync(response, operationId);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    [Fact]
    public async Task ガントの日程は期間と重なるタスクと日付未設定のタスクを契約どおりに返す()
    {
        var (client, owner, project) = await NewProjectAsync();
        await factory.Data.CreateTaskAsync(project, owner, "見積書の作成", startDate: D("2026-10-01"), dueDate: D("2026-10-09"));
        await factory.Data.CreateTaskAsync(project, owner, "期間外", dueDate: D("2027-03-01"));
        await factory.Data.CreateTaskAsync(project, owner, "日付なし");

        using var response = await client.GetAsync($"/api/v1/projects/{project.Key}/tasks/schedule?from=2026-10-05&to=2026-12-27", Ct);
        var body = await OkAsync(response, "GetTaskSchedule");

        var item = Assert.Single(body.GetProperty("items").EnumerateArray());
        Assert.Equal("見積書の作成", item.GetProperty("title").GetString());
        Assert.Equal("2026-10-01", item.GetProperty("startDate").GetString());
        Assert.Equal("2026-10-09", item.GetProperty("dueDate").GetString());
        Assert.Equal(1, body.GetProperty("undatedCount").GetInt32());
        Assert.Equal("日付なし", Assert.Single(body.GetProperty("undatedItems").EnumerateArray()).GetProperty("title").GetString());
        Assert.False(body.GetProperty("truncated").GetBoolean());
        Assert.Equal("2026-10-05", body.GetProperty("from").GetString());
    }

    [Theory]
    [InlineData("", "from")]
    [InlineData("?from=2026-10-01", "to")]
    [InlineData("?from=2026-10-08&to=2026-10-07", "to")]
    [InlineData("?from=2026-01-01&to=2027-01-02", "to")]
    [InlineData("?from=2026-10-01&to=2026-13-01", "to")]
    [InlineData("?from=1999-12-31&to=2000-01-31", "from")]
    public async Task ガントの期間が不正なら項目ごとの400(string query, string field)
    {
        var (client, _, project) = await NewProjectAsync();

        using var response = await client.GetAsync($"/api/v1/projects/{project.Key}/tasks/schedule{query}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        await OpenApiContract.AssertResponseAsync(response, "GetTaskSchedule");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("validation.failed", body.GetProperty("code").GetString());
        var message = body.GetProperty("errors").GetProperty(field)[0].GetString()!;
        Assert.NotEqual(Encoding.UTF8.GetByteCount(message), message.Length); // 日本語の文言
    }

    [Fact]
    public async Task ガントで変えた日程は既存の更新APIで版を確認して保存され競合は409()
    {
        var (client, owner, project) = await NewProjectAsync();
        var task = await factory.Data.CreateTaskAsync(project, owner, "日程を動かす", startDate: D("2026-10-01"), dueDate: D("2026-10-09"));
        var url = $"/api/v1/projects/{project.Key}/tasks/{task.Number}";

        // ドラッグで 3 日後ろへ移動（開始日と期限を同じ日数ずらす）
        using var moved = await client.PatchAsJsonAsync(url, new { startDate = "2026-10-04", dueDate = "2026-10-12", version = 0 }, Ct);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        // 古い版で送ると競合
        using var stale = await client.PatchAsJsonAsync(url, new { dueDate = "2026-10-20", version = 0 }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency.conflict", (await stale.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
        // 期限を開始日より前にはできない（TK-02）
        using var invalid = await client.PatchAsJsonAsync(url, new { dueDate = "2026-10-01", version = 1 }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        using var schedule = await client.GetAsync($"/api/v1/projects/{project.Key}/tasks/schedule?from=2026-10-01&to=2026-10-31", Ct);
        var item = Assert.Single((await OkAsync(schedule, "GetTaskSchedule")).GetProperty("items").EnumerateArray());
        Assert.Equal(("2026-10-04", "2026-10-12", 1), (item.GetProperty("startDate").GetString(), item.GetProperty("dueDate").GetString(), item.GetProperty("version").GetInt32()));
    }

    [Fact]
    public async Task プロジェクトの円グラフは全ステータスを件数と割合で返す()
    {
        var (client, owner, project) = await NewProjectAsync();
        await factory.Data.CreateTaskAsync(project, owner, "未着手");
        await factory.Data.CreateTaskAsync(project, owner, "未着手 2");
        await factory.Data.CreateTaskAsync(project, owner, "完了", category: StatusCategory.Done);

        using var response = await client.GetAsync($"/api/v1/projects/{project.Key}/tasks/status-summary", Ct);
        var body = await OkAsync(response, "GetTaskStatusSummary");

        Assert.Equal("status", body.GetProperty("groupedBy").GetString());
        Assert.Equal(3, body.GetProperty("totalCount").GetInt32());
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(["未着手", "対応中", "確認待ち", "完了", "中止"], items.Select(i => i.GetProperty("name").GetString()));
        Assert.Equal([2, 0, 0, 1, 0], items.Select(i => i.GetProperty("count").GetInt32()));
        Assert.Equal([66.7m, 0m, 0m, 33.3m, 0m], items.Select(i => i.GetProperty("percent").GetDecimal()));
        Assert.Equal(["gray", "blue", "purple", "green", "red"], items.Select(i => i.GetProperty("color").GetString()));
    }

    [Fact]
    public async Task 自分の円グラフは区分別とプロジェクト指定のステータス別を返す()
    {
        var (client, owner, project) = await NewProjectAsync();
        await factory.Data.CreateTaskAsync(project, owner, "自分の担当", owner.Id, category: StatusCategory.InProgress);
        await factory.Data.CreateTaskAsync(project, owner, "担当なし");

        using var all = await client.GetAsync("/api/v1/me/tasks/status-summary", Ct);
        var allBody = await OkAsync(all, "GetMyTaskStatusSummary");
        using var byProject = await client.GetAsync($"/api/v1/me/tasks/status-summary?projectKey={project.Key}", Ct);
        var projectBody = await OkAsync(byProject, "GetMyTaskStatusSummary");

        Assert.Equal("category", allBody.GetProperty("groupedBy").GetString());
        Assert.Equal(["未着手", "進行中", "完了", "中止"], allBody.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("name").GetString()));
        Assert.All(allBody.GetProperty("items").EnumerateArray(), i => Assert.Equal(JsonValueKind.Null, i.GetProperty("statusId").ValueKind));
        Assert.Equal(1, allBody.GetProperty("totalCount").GetInt32());
        Assert.Equal("status", projectBody.GetProperty("groupedBy").GetString());
        Assert.Equal(1, projectBody.GetProperty("totalCount").GetInt32());
        Assert.Equal(100.0m, projectBody.GetProperty("items")[1].GetProperty("percent").GetDecimal());
    }

    [Fact]
    public async Task 自分の円グラフに他のプロジェクトのキーを指定しても他人の件数は返らない()
    {
        // IDOR: 参加していないプロジェクト B を指定 → 404。B の件数（自分が担当として登録されたものも含む）は返さない
        var (client, owner, _) = await NewProjectAsync();
        var ownerB = await factory.Data.CreateUserAsync();
        var b = await factory.Data.CreateProjectAsync(ownerB);
        await factory.Data.CreateTaskAsync(b, ownerB, "B の機密タスク", ownerB.Id);
        await factory.Data.CreateTaskAsync(b, ownerB, "B で自分が担当になっている", owner.Id);

        using var mine = await client.GetAsync($"/api/v1/me/tasks/status-summary?projectKey={b.Key}", Ct);
        using var projectSummary = await client.GetAsync($"/api/v1/projects/{b.Key}/tasks/status-summary", Ct);
        using var schedule = await client.GetAsync($"/api/v1/projects/{b.Key}/tasks/schedule?from=2026-10-01&to=2026-10-31", Ct);
        using var all = await client.GetAsync("/api/v1/me/tasks/status-summary", Ct);

        foreach (var response in new[] { mine, projectSummary, schedule })
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("resource.not_found", (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
        }

        Assert.Equal(0, (await OkAsync(all, "GetMyTaskStatusSummary")).GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData("' OR 1=1 --")]
    [InlineData("A")]
    [InlineData("TOOLONGPROJECTKEY")]
    public async Task 自分の円グラフのプロジェクトキーの形式が不正なら400(string projectKey)
    {
        var (client, _, _) = await NewProjectAsync();

        using var response = await client.GetAsync($"/api/v1/me/tasks/status-summary?projectKey={Uri.EscapeDataString(projectKey)}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await OpenApiContract.AssertResponseAsync(response, "GetMyTaskStatusSummary");
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors").TryGetProperty("projectKey", out _));
    }
}
