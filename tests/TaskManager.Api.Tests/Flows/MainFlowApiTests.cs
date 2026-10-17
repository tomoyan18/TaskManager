using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TaskManager.Api.Tests.Authorization;
using TaskManager.Api.Tests.Support;
using TaskManager.Testing;

namespace TaskManager.Api.Tests.Flows;

/// <summary>
/// 主要な業務の流れを HTTP 経由で通す（UF-01〜05 相当）。各応答を OpenAPI の契約と照合する。
/// </summary>
public class MainFlowApiTests(ApiFactory factory)
{
    private static readonly string[] BucketNames = ["overdue", "today", "thisWeek", "later", "noDueDate"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<JsonElement> OkAsync(HttpResponseMessage response, string operationId, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == expected, $"{operationId}: 期待 {expected}、実際 {(int)response.StatusCode} {text}");
        await OpenApiContract.AssertResponseAsync(response, operationId);
        return text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
    }

    private async Task<(HttpClient Client, string AccessToken)> LoginAsync(string email, string password)
    {
        var client = factory.CreateAnonymousClient();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, Ct);
        var body = await OkAsync(response, "Login");
        var token = body.GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, token);
    }

    [Fact]
    public async Task 利用者の発行からタスクの完了までの流れ()
    {
        var admin = await factory.Data.CreateUserAsync(administrator: true);
        using var adminClient = await factory.CreateClientAsync(admin);

        // 管理者が利用者を 2 人発行する（一時パスワード）
        var ownerEmail = Unique.Email("owner");
        var memberEmail = Unique.Email("member");
        var ownerTemp = (await OkAsync(await adminClient.PostAsJsonAsync("/api/v1/users",
            new { email = ownerEmail, displayName = "山田 太郎", systemRole = "member" }, Ct), "CreateUser", HttpStatusCode.Created))
            .GetProperty("temporaryPassword").GetString()!;
        var memberTemp = (await OkAsync(await adminClient.PostAsJsonAsync("/api/v1/users",
            new { email = memberEmail, displayName = "佐藤 花子", systemRole = "member" }, Ct), "CreateUser", HttpStatusCode.Created))
            .GetProperty("temporaryPassword").GetString()!;

        // 一時パスワードでログインし、パスワードを変更する
        var (ownerClient, _) = await LoginAsync(ownerEmail, ownerTemp);
        using var _o = ownerClient;
        var changed = await OkAsync(await ownerClient.PostAsJsonAsync("/api/v1/auth/password",
            new { currentPassword = ownerTemp, newPassword = "Owner-Password-2026" }, Ct), "ChangePassword");
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", changed.GetProperty("accessToken").GetString());
        var (memberClient, _) = await LoginAsync(memberEmail, memberTemp);
        using var _m = memberClient;
        changed = await OkAsync(await memberClient.PostAsJsonAsync("/api/v1/auth/password",
            new { currentPassword = memberTemp, newPassword = "Member-Password-2026" }, Ct), "ChangePassword");
        memberClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", changed.GetProperty("accessToken").GetString());
        var me = await OkAsync(await memberClient.GetAsync("/api/v1/me", Ct), "GetMe");
        var memberId = me.GetProperty("id").GetGuid();

        // プロジェクトを作り、メンバーを追加する
        var key = Unique.ProjectKey();
        var project = await OkAsync(await ownerClient.PostAsJsonAsync("/api/v1/projects",
            new { key = key.ToLowerInvariant(), name = Unique.Name("営業支援"), description = "**顧客**対応の管理" }, Ct), "CreateProject", HttpStatusCode.Created);
        Assert.Equal(key, project.GetProperty("key").GetString());
        var directory = await OkAsync(await ownerClient.GetAsync($"/api/v1/user-directory?q={Uri.EscapeDataString("佐藤")}&excludeProjectKey={key}", Ct), "SearchUserDirectory");
        Assert.Contains(directory.EnumerateArray(), u => u.GetProperty("id").GetGuid() == memberId);
        await OkAsync(await ownerClient.PostAsJsonAsync($"/api/v1/projects/{key}/members", new { userId = memberId, role = "editor" }, Ct),
            "AddProjectMember", HttpStatusCode.Created);

        // タスクを作り、担当者と期限を設定する
        var task = await OkAsync(await ownerClient.PostAsJsonAsync($"/api/v1/projects/{key}/tasks", new { title = "見積書の作成" }, Ct),
            "CreateTask", HttpStatusCode.Created);
        var number = task.GetProperty("number").GetInt32();
        task = await OkAsync(await ownerClient.PatchAsJsonAsync($"/api/v1/projects/{key}/tasks/{number}",
            new { assigneeUserId = memberId, dueDate = "2026-10-31", priority = "high", version = task.GetProperty("version").GetUInt32() }, Ct), "UpdateTask");
        Assert.Equal("2026-10-31", task.GetProperty("dueDate").GetString());

        // 担当者は自分のタスクとお知らせで確認できる
        var myTasks = await OkAsync(await memberClient.GetAsync("/api/v1/me/tasks", Ct), "GetMyTasks");
        var buckets = myTasks.GetProperty("buckets");
        Assert.Contains(BucketNames.SelectMany(b => buckets.GetProperty(b).EnumerateArray()),
            t => t.GetProperty("key").GetString() == $"{key}-{number}");
        var unread = await OkAsync(await memberClient.GetAsync("/api/v1/me/notices/unread-count", Ct), "GetMyUnreadNoticeCount");
        Assert.True(unread.GetProperty("count").GetInt32() >= 2); // プロジェクトへの追加 + 担当
        var notices = await OkAsync(await memberClient.GetAsync("/api/v1/me/notices?unreadOnly=true", Ct), "ListMyNotices");
        foreach (var n in notices.GetProperty("items").EnumerateArray())
        {
            var link = n.GetProperty("linkPath").GetString();
            Assert.True(link is null || link.StartsWith('/'), link);
        }

        await OkAsync(await memberClient.PostAsync("/api/v1/me/notices/read-all", null, Ct), "MarkAllNoticesRead", HttpStatusCode.NoContent);

        // 担当者がコメント・添付をして完了にする
        await OkAsync(await memberClient.PostAsJsonAsync($"/api/v1/projects/{key}/tasks/{number}/comments", new { body = "単価表を**共有**します。" }, Ct),
            "CreateTaskComment", HttpStatusCode.Created);
        var attachment = await OkAsync(await memberClient.SendAsync(AuthorizationMatrix.Upload(
            $"/api/v1/projects/{key}/tasks/{number}/attachments", "単価表.csv", "品目,単価\n部品A,100"), Ct), "UploadTaskAttachment", HttpStatusCode.Created);
        Assert.Equal("text/csv", attachment.GetProperty("contentType").GetString());
        using var download = await memberClient.GetAsync(
            $"/api/v1/projects/{key}/tasks/{number}/attachments/{attachment.GetProperty("id").GetGuid()}/content", Ct);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("品目,単価\n部品A,100", await download.Content.ReadAsStringAsync(Ct));

        var statuses = await OkAsync(await memberClient.GetAsync($"/api/v1/projects/{key}/statuses", Ct), "ListWorkflowStatuses");
        var done = statuses.EnumerateArray().First(s => s.GetProperty("category").GetString() == "done").GetProperty("id").GetGuid();
        task = await OkAsync(await memberClient.GetAsync($"/api/v1/projects/{key}/tasks/{number}", Ct), "GetTask");
        task = await OkAsync(await memberClient.PatchAsJsonAsync($"/api/v1/projects/{key}/tasks/{number}",
            new { statusId = done, version = task.GetProperty("version").GetUInt32() }, Ct), "UpdateTask");
        Assert.NotEqual(JsonValueKind.Null, task.GetProperty("completedAtUtc").ValueKind);
        Assert.Equal(1, task.GetProperty("commentCount").GetInt32());
        Assert.Equal(1, task.GetProperty("attachmentCount").GetInt32());

        // 作成者に完了のお知らせが届き、履歴に経過が残る
        var ownerNotices = await OkAsync(await ownerClient.GetAsync("/api/v1/me/notices", Ct), "ListMyNotices");
        Assert.Contains(ownerNotices.GetProperty("items").EnumerateArray(), n => n.GetProperty("kind").GetString() == "taskCompleted");
        var history = await OkAsync(await ownerClient.GetAsync($"/api/v1/projects/{key}/tasks/{number}/history", Ct), "ListTaskHistory");
        var kinds = history.GetProperty("items").EnumerateArray().Select(h => h.GetProperty("kind").GetString()).ToHashSet();
        Assert.Superset(new HashSet<string?> { "created", "assigneeChanged", "dueDateChanged", "statusChanged", "attachmentAdded" }, kinds);

        // 一覧・検索・プロジェクトの集計
        var list = await OkAsync(await ownerClient.GetAsync($"/api/v1/projects/{key}/tasks?includeClosed=true&sort=-number", Ct), "ListTasks");
        Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
        var search = await OkAsync(await ownerClient.GetAsync($"/api/v1/search/tasks?q={key}-{number}", Ct), "SearchTasks");
        Assert.Equal($"{key}-{number}", search[0].GetProperty("key").GetString());
        var detail = await OkAsync(await ownerClient.GetAsync($"/api/v1/projects/{key}", Ct), "GetProject");
        Assert.Equal(1, detail.GetProperty("taskCounts").GetProperty("done").GetInt32());
        Assert.Equal(2, detail.GetProperty("memberCount").GetInt32());
    }
}
