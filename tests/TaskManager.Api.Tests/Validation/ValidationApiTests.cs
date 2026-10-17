using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TaskManager.Api.Tests.Support;
using TaskManager.Testing;

namespace TaskManager.Api.Tests.Validation;

/// <summary>入力検証と Problem Details（api-design.md §1.5、testing-strategy.md §4「入力検証」）。</summary>
public class ValidationApiTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<(HttpClient Client, TestProject Project)> NewProjectAsync()
    {
        var owner = await factory.Data.CreateUserAsync();
        var project = await factory.Data.CreateProjectAsync(owner);
        return (await factory.CreateClientAsync(owner), project);
    }

    private static async Task<JsonElement> AssertValidationProblemAsync(HttpResponseMessage response, string operationId, string? field = null)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        await OpenApiContract.AssertResponseAsync(response, operationId);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        if (field is not null)
        {
            Assert.Equal("validation.failed", body.GetProperty("code").GetString());
            var messages = body.GetProperty("errors").GetProperty(field);
            Assert.Equal(1, messages.GetArrayLength());
            var message = messages[0].GetString()!;
            // 利用者向けの文言は日本語（英語のフレームワーク既定メッセージを出さない）
            Assert.NotEqual(Encoding.UTF8.GetByteCount(message), message.Length);
        }

        return body;
    }

    private static StringContent RawJson(string json) => new(json, Encoding.UTF8, "application/json");

    [Theory]
    [InlineData("""{"title":""}""", "title")]
    [InlineData("""{"title":"   "}""", "title")]
    [InlineData("""{"priority":"normal"}""", "title")]
    [InlineData("""{"title":"a","priority":"highest"}""", "priority")]
    [InlineData("""{"title":"a","priority":3}""", "priority")]
    [InlineData("""{"title":"a","dueDate":"2026/10/09"}""", "dueDate")]
    [InlineData("""{"title":"a","dueDate":"2026-02-30"}""", "dueDate")]
    [InlineData("""{"title":"a","startDate":"2026-10-10","dueDate":"2026-10-09"}""", "dueDate")]
    [InlineData("""{"title":"a","assigneeUserId":"not-a-guid"}""", "assigneeUserId")]
    [InlineData("""{"title":"a","parentTaskNumber":"1"}""", "parentTaskNumber")]
    [InlineData("""{"title":"a","parentTaskNumber":0}""", "parentTaskNumber")]
    [InlineData("""{"title":"a","dueDate":"1999-12-31"}""", "dueDate")]
    public async Task タスク作成の不正な入力は項目ごとの400(string json, string field)
    {
        var (client, project) = await NewProjectAsync();
        using var response = await client.PostAsync($"/api/v1/projects/{project.Key}/tasks", RawJson(json), Ct);
        await AssertValidationProblemAsync(response, "CreateTask", field);
    }

    [Fact]
    public async Task タイトルは200文字まで受け付け201文字は400()
    {
        var (client, project) = await NewProjectAsync();
        using var ok = await client.PostAsJsonAsync($"/api/v1/projects/{project.Key}/tasks", new { title = new string('あ', 200) }, Ct);
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        await OpenApiContract.AssertResponseAsync(ok, "CreateTask");
        Assert.NotNull(ok.Headers.Location);

        using var tooLong = await client.PostAsJsonAsync($"/api/v1/projects/{project.Key}/tasks", new { title = new string('あ', 201) }, Ct);
        await AssertValidationProblemAsync(tooLong, "CreateTask", "title");
    }

    [Theory]
    [InlineData("{")]
    [InlineData("[1,2,3]")]
    [InlineData("not json")]
    public async Task JSONとして読めない本文は400(string json)
    {
        var (client, project) = await NewProjectAsync();
        using var response = await client.PostAsync($"/api/v1/projects/{project.Key}/tasks", RawJson(json), Ct);
        await AssertValidationProblemAsync(response, "CreateTask");
    }

    [Fact]
    public async Task 深すぎる入れ子のJSONは400()
    {
        var (client, project) = await NewProjectAsync();
        var nested = string.Concat(Enumerable.Repeat("{\"a\":", 40)) + "1" + new string('}', 40);
        using var response = await client.PostAsync($"/api/v1/projects/{project.Key}/tasks", RawJson($"{{\"title\":\"x\",\"extra\":{nested}}}"), Ct);
        await AssertValidationProblemAsync(response, "CreateTask");
    }

    [Fact]
    public async Task 未知のプロパティは無視する()
    {
        var (client, project) = await NewProjectAsync();
        using var response = await client.PostAsync($"/api/v1/projects/{project.Key}/tasks",
            RawJson("""{"title":"未知の項目つき","projectId":"00000000-0000-0000-0000-000000000000","number":999,"isAdmin":true}"""), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(1, body.GetProperty("number").GetInt32());
    }

    [Theory]
    [InlineData("""{"title":"版なし"}""")]
    [InlineData("""{"title":"文字列の版","version":"0"}""")]
    [InlineData("""{"title":"負の版","version":-1}""")]
    public async Task 更新の版は数値で必須(string json)
    {
        var (client, project) = await NewProjectAsync();
        var owner = await factory.Data.CreateUserAsync();
        await factory.Data.CreateTaskAsync(project, owner);

        using var response = await client.PatchAsync($"/api/v1/projects/{project.Key}/tasks/1", RawJson(json), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=0")]
    [InlineData("priority=highest")]
    [InlineData("dueFrom=2026-13-01")]
    [InlineData("sort=passwordHash")]
    [InlineData("statusId=not-a-guid")]
    public async Task 一覧の不正なクエリは400(string query)
    {
        var (client, project) = await NewProjectAsync();
        using var response = await client.GetAsync($"/api/v1/projects/{project.Key}/tasks?{query}", Ct);
        await AssertValidationProblemAsync(response, "ListTasks");
    }

    [Fact]
    public async Task 存在しないタスク番号は404でProblemDetails()
    {
        var (client, project) = await NewProjectAsync();
        using var response = await client.GetAsync($"/api/v1/projects/{project.Key}/tasks/999999", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await OpenApiContract.AssertResponseAsync(response, "GetTask");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("resource.not_found", body.GetProperty("code").GetString());
        Assert.Equal("対象が見つかりません。", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task 楽観的排他の競合は409()
    {
        var (client, project) = await NewProjectAsync();
        using var created = await client.PostAsJsonAsync($"/api/v1/projects/{project.Key}/tasks", new { title = "競合" }, Ct);
        var version = (await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("version").GetUInt32();

        using var first = await client.PatchAsJsonAsync($"/api/v1/projects/{project.Key}/tasks/1", new { title = "1回目", version }, Ct);
        using var second = await client.PatchAsJsonAsync($"/api/v1/projects/{project.Key}/tasks/1", new { title = "2回目", version }, Ct);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        await OpenApiContract.AssertResponseAsync(first, "UpdateTask");
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        await OpenApiContract.AssertResponseAsync(second, "UpdateTask");
        Assert.Equal("concurrency.conflict", (await second.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task 検索語の記号やSQL断片はそのまま文字として扱う()
    {
        var (client, project) = await NewProjectAsync();
        var owner = await factory.Data.CreateUserAsync();
        await factory.Data.CreateTaskAsync(project, owner, "通常のタスク");
        await factory.Data.CreateTaskAsync(project, owner, "達成率100%");

        foreach (var (q, expected) in new[] { ("' OR 1=1 --", 0), ("%", 1), ("_", 0), ("\\", 0), ("\"; DROP TABLE task_item; --", 0) })
        {
            using var response = await client.GetAsync($"/api/v1/projects/{project.Key}/tasks?q={Uri.EscapeDataString(q)}", Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(expected, (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("totalCount").GetInt32());
        }

        using var search = await client.GetAsync($"/api/v1/search/tasks?q={Uri.EscapeDataString("' OR 1=1 --")}", Ct);
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        Assert.Equal(0, (await search.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetArrayLength());
    }
}
