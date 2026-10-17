using System.Net;
using System.Text.Json.Nodes;
using TaskManager.Api.Tests.Support;

namespace TaskManager.Api.Tests.OpenApi;

/// <summary>OpenAPI 契約テスト（testing-strategy.md §6）。</summary>
public class OpenApiDocumentTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly HashSet<string> AnonymousOperations = ["Login", "GetSystemInfo"];
    private static readonly HashSet<string> CookieOperations = ["RefreshAccessToken", "Logout"];

    private static JsonNode Normalize(JsonNode node)
    {
        var copy = JsonNode.Parse(node.ToJsonString())!.AsObject();
        copy.Remove("servers");
        return Sort(copy)!;
    }

    private static JsonNode? Sort(JsonNode? node) => node switch
    {
        JsonObject obj => new JsonObject(obj.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => KeyValuePair.Create(p.Key, Sort(p.Value)))),
        JsonArray array => new JsonArray(array.Select(Sort).ToArray()),
        _ => node?.DeepClone(),
    };

    [Fact]
    public async Task 実行中のAPIが公開するOpenAPI文書はコミット済みのスナップショットと一致する()
    {
        using var client = factory.CreateAnonymousClient();
        using var response = await client.GetAsync("/openapi/v1.json", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var served = JsonNode.Parse(await response.Content.ReadAsStringAsync(Ct))!;

        var expected = Normalize(OpenApiContract.Document).ToJsonString();
        var actual = Normalize(served).ToJsonString();

        Assert.True(expected == actual,
            "OpenAPI 文書がスナップショット（docs/api/openapi.v1.json）と異なります。api-design.md とスナップショットを更新してください。");
    }

    [Fact]
    public void ビルド時に生成したOpenAPI文書もスナップショットと一致する()
    {
        var generated = Path.Combine(OpenApiContract.RepositoryRoot, "artifacts", "openapi", "openapi.json");
        Assert.SkipUnless(File.Exists(generated), "artifacts/openapi/openapi.json がありません（API のビルドで生成されます）。");

        var actual = Normalize(JsonNode.Parse(File.ReadAllText(generated))!).ToJsonString();
        Assert.Equal(Normalize(OpenApiContract.Document).ToJsonString(), actual);
    }

    [Fact]
    public void 全操作にタグ_要約_operationIdがあり匿名で呼べるのはログインとシステム情報だけ()
    {
        var operations = OpenApiContract.Operations().ToList();
        // Phase 9 でガント・円グラフの 3 操作を追加（61 → 64。api-design.md §3 #62〜#64）
        Assert.Equal(64, operations.Count);
        foreach (var (id, method, path, op) in operations)
        {
            Assert.True(op["tags"] is JsonArray { Count: > 0 }, $"{id}: tags");
            Assert.False(string.IsNullOrWhiteSpace(op["summary"]?.GetValue<string>()), $"{id}: summary");
            Assert.StartsWith("/api/v1/", path, StringComparison.Ordinal);
            var security = op["security"] as JsonArray;
            if (AnonymousOperations.Contains(id))
            {
                Assert.True(security is null || security.Count == 0, $"{id} は匿名で呼べる");
            }
            else if (CookieOperations.Contains(id))
            {
                Assert.Equal("refreshCookie", Assert.Single(security!)!.AsObject().Single().Key);
            }
            else
            {
                Assert.True(security is { Count: > 0 } && security[0]!.AsObject().ContainsKey("bearerAuth"), $"{method} {path} に bearerAuth がない");
            }
        }
    }

    [Fact]
    public void 全操作が500とエラー応答をProblemDetailsで定義する()
    {
        foreach (var (id, _, _, op) in OpenApiContract.Operations())
        {
            var responses = op["responses"]!.AsObject();
            Assert.True(responses.ContainsKey("500"), $"{id}: 500");
            if (!AnonymousOperations.Contains(id) && !CookieOperations.Contains(id))
            {
                Assert.True(responses.ContainsKey("401"), $"{id}: 401");
            }

            foreach (var (status, response) in responses.Where(r => r.Key.StartsWith('4') || r.Key.StartsWith('5')))
            {
                Assert.True(response!["content"]?["application/problem+json"] is not null, $"{id} {status}: application/problem+json");
            }
        }
    }

    [Theory]
    [InlineData("CreateTaskRequest", "title", 1, 200)]
    [InlineData("CreateProjectRequest", "key", 2, 10)]
    [InlineData("CreateProjectRequest", "name", 1, 80)]
    [InlineData("CreateTaskCommentRequest", "body", 1, 10000)]
    [InlineData("ChangePasswordRequest", "newPassword", 12, 128)]
    [InlineData("LoginRequest", "email", 1, 254)]
    public void 要求のスキーマに文字数の制約がある(string schema, string property, int min, int max)
    {
        var p = OpenApiContract.Document["components"]!["schemas"]![schema]!["properties"]![property]!;
        Assert.Equal(min, p["minLength"]!.GetValue<int>());
        Assert.Equal(max, p["maxLength"]!.GetValue<int>());
    }

    [Fact]
    public void 更新要求の版は必須()
    {
        foreach (var schema in new[] { "UpdateTaskRequest", "UpdateProjectRequest", "UpdateMilestoneRequest", "UpdateUserRequest" })
        {
            var required = OpenApiContract.Document["components"]!["schemas"]![schema]!["required"]!.AsArray().Select(r => r!.GetValue<string>());
            Assert.Contains("version", required);
        }
    }

    [Theory]
    [InlineData("plane")]
    [InlineData("signingkey")]
    [InlineData("connectionstring")]
    [InlineData("password=")]
    [InlineData("/volumes/")]
    [InlineData("/users/tomoya")]
    [InlineData("exception")]
    [InlineData("stacktrace")]
    [InlineData("mysql")]
    public void 文書に秘密情報や内部情報を含まない(string word)
    {
        var text = File.ReadAllText(OpenApiContract.SnapshotPath).ToLowerInvariant();
        Assert.DoesNotContain(word, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SwaggerUIとOpenAPI文書は有効時にログインなしで表示できる()
    {
        using var client = factory.CreateAnonymousClient();
        using var ui = await client.GetAsync("/swagger/index.html", Ct);
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
        using var doc = await client.GetAsync("/openapi/v1.json", Ct);
        Assert.Equal(HttpStatusCode.OK, doc.StatusCode);
    }

    [Fact]
    public void 検証器は型の違いと必須項目の欠落と文書にない項目を検出する()
    {
        var ok = JsonNode.Parse("""{"id":"0193a9f4-1b1c-7d3e-9f00-000000000001","displayName":"テスト","email":"a@example.test"}""");
        Assert.Empty(OpenApiContract.ValidateAgainst("UserDirectoryEntry", ok));
        Assert.NotEmpty(OpenApiContract.ValidateAgainst("UserDirectoryEntry", JsonNode.Parse("""{"id":"x","displayName":"テスト","email":"a@example.test"}""")));
        Assert.NotEmpty(OpenApiContract.ValidateAgainst("UserDirectoryEntry", JsonNode.Parse("""{"displayName":"テスト","email":"a@example.test"}""")));
        Assert.NotEmpty(OpenApiContract.ValidateAgainst("UserDirectoryEntry", JsonNode.Parse(
            """{"id":"0193a9f4-1b1c-7d3e-9f00-000000000001","displayName":"テスト","email":"a@example.test","passwordHash":"x"}""")));
        Assert.NotEmpty(OpenApiContract.ValidateAgainst("UserDirectoryEntry", JsonNode.Parse(
            """{"id":"0193a9f4-1b1c-7d3e-9f00-000000000001","displayName":1,"email":"a@example.test"}""")));
    }
}
