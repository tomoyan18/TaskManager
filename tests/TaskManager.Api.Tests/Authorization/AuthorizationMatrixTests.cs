using System.Net.Http.Json;
using System.Text.Json;
using TaskManager.Api.Tests.Support;

namespace TaskManager.Api.Tests.Authorization;

/// <summary>全エンドポイント × 呼び出し元の認可（testing-strategy.md §5）。応答は OpenAPI の契約とも照合する。</summary>
public class AuthorizationMatrixTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void OpenAPI文書の全operationIdがマトリクスにある()
    {
        var documented = OpenApiContract.Operations().Select(o => o.OperationId).Order().ToList();
        var covered = AuthorizationMatrix.Endpoints.Select(e => e.OperationId).Order().ToList();

        Assert.Equal(documented, covered);
    }

    [Fact]
    public void 許可された場合のステータスは文書に定義されている()
    {
        foreach (var endpoint in AuthorizationMatrix.Endpoints)
        {
            var responses = OpenApiContract.Operation(endpoint.OperationId)["responses"]!.AsObject();
            Assert.True(responses.ContainsKey(endpoint.AllowedStatus.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                $"{endpoint.OperationId}: {endpoint.AllowedStatus}");
        }
    }

    [Theory]
    [MemberData(nameof(AuthorizationMatrix.Cases), MemberType = typeof(AuthorizationMatrix))]
    public async Task 呼び出し元ごとに期待どおりのステータスを返す(string operationId, Caller caller)
    {
        var endpoint = AuthorizationMatrix.Find(operationId);
        var world = await AuthorizationWorld.CreateAsync(factory, archived: caller == Caller.ArchivedProjectOwner);
        var user = world.UserFor(caller);
        using var client = user is null ? factory.CreateAnonymousClient() : await factory.CreateClientAsync(user);
        using var request = endpoint.Build(world);

        using var response = await client.SendAsync(request, Ct);

        var expected = AuthorizationMatrix.Expected(endpoint.Rule, caller, endpoint.AllowedStatus);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True((int)response.StatusCode == expected,
            $"{endpoint.OperationId} / {caller}: 期待 {expected}、実際 {(int)response.StatusCode} {body}");
        await OpenApiContract.AssertResponseAsync(response, endpoint.OperationId);

        if (expected >= 400)
        {
            // エラーは Problem Details（api-design.md §1.4）
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            using var problem = JsonDocument.Parse(body);
            var code = problem.RootElement.GetProperty("code").GetString();
            var expectedCode = (expected, caller) switch
            {
                (401, Caller.Anonymous) when endpoint.Rule != AccessRule.Public => "auth.unauthenticated",
                (403, Caller.PasswordChangeRequired) when endpoint.Rule is not AccessRule.SystemAdministrator => "auth.password_change_required",
                (404, Caller.NonMember or Caller.SystemAdministrator) => "resource.not_found",
                (409, Caller.ArchivedProjectOwner) => "project.archived",
                _ => null,
            };
            if (expectedCode is not null)
            {
                Assert.Equal(expectedCode, code);
            }
        }
    }

    [Fact]
    public async Task システム管理者はアーカイブ済みのプロジェクトを削除できる()
    {
        var world = await AuthorizationWorld.CreateAsync(factory, archived: true);
        using var client = await factory.CreateClientAsync(world.Administrator);

        using var wrongKey = await client.DeleteAsync($"/api/v1/projects/{world.ProjectKey}?confirmKey=WRONG", Ct);
        Assert.Equal(400, (int)wrongKey.StatusCode);
        using var response = await client.DeleteAsync($"/api/v1/projects/{world.ProjectKey}?confirmKey={world.ProjectKey}", Ct);
        Assert.Equal(204, (int)response.StatusCode);

        using var ownerClient = await factory.CreateClientAsync(world.Owner);
        using var gone = await ownerClient.GetAsync($"/api/v1/projects/{world.ProjectKey}", Ct);
        Assert.Equal(404, (int)gone.StatusCode);
    }

    [Fact]
    public async Task 編集者は自分が作ったタスクとコメントと添付を削除できる()
    {
        var world = await AuthorizationWorld.CreateAsync(factory, archived: false);
        using var client = await factory.CreateClientAsync(world.Editor);
        var baseUrl = $"/api/v1/projects/{world.ProjectKey}/tasks";

        using var created = await client.PostAsJsonAsync(baseUrl, new { title = "編集者のタスク" }, Ct);
        var number = (await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("number").GetInt32();
        using var comment = await client.PostAsJsonAsync($"{baseUrl}/{number}/comments", new { body = "編集者のコメント" }, Ct);
        var commentId = (await comment.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
        using var upload = await client.SendAsync(AuthorizationMatrix.Upload($"{baseUrl}/{number}/attachments", "memo.txt", "編集者の添付"), Ct);
        var attachmentId = (await upload.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();

        using var editComment = await client.PatchAsJsonAsync($"{baseUrl}/{number}/comments/{commentId}", new { body = "自分のコメントは編集できる" }, Ct);
        Assert.Equal(200, (int)editComment.StatusCode);
        Assert.Equal(204, (int)(await client.DeleteAsync($"{baseUrl}/{number}/attachments/{attachmentId}", Ct)).StatusCode);
        Assert.Equal(204, (int)(await client.DeleteAsync($"{baseUrl}/{number}/comments/{commentId}", Ct)).StatusCode);
        Assert.Equal(204, (int)(await client.DeleteAsync($"{baseUrl}/{number}", Ct)).StatusCode);
    }

    [Fact]
    public async Task メンバーは自分でプロジェクトから脱退できる()
    {
        var world = await AuthorizationWorld.CreateAsync(factory, archived: false);
        using var client = await factory.CreateClientAsync(world.Viewer);

        using var leave = await client.DeleteAsync($"/api/v1/projects/{world.ProjectKey}/members/{world.Viewer.Id}", Ct);
        Assert.Equal(204, (int)leave.StatusCode);
        using var after = await client.GetAsync($"/api/v1/projects/{world.ProjectKey}", Ct);
        Assert.Equal(404, (int)after.StatusCode);
    }
}
