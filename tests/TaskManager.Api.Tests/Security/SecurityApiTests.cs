using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TaskManager.Api.Tests.Authorization;
using TaskManager.Api.Tests.Support;

namespace TaskManager.Api.Tests.Security;

/// <summary>セキュリティ上の応答（ヘッダー、エラー情報の漏えい、ダウンロード）。</summary>
public class SecurityApiTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/api/v1/system/info")]
    [InlineData("/api/v1/me")]
    public async Task API応答にセキュリティヘッダーを付ける(string url)
    {
        using var client = factory.CreateAnonymousClient();
        using var response = await client.GetAsync(url, Ct);

        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("strict-origin-when-cross-origin", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.Equal("default-src 'none'; frame-ancestors 'none'", Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);
        Assert.False(response.Headers.Contains("Server"));
        Assert.False(response.Headers.Contains("X-Powered-By"));
    }

    [Fact]
    public async Task 想定外の例外は500で内部情報を返さず追跡IDを返す()
    {
        var owner = await factory.Data.CreateUserAsync();
        var project = await factory.Data.CreateProjectAsync(owner);
        var task = await factory.Data.CreateTaskAsync(project, owner);
        using var client = await factory.CreateClientAsync(owner);

        using var response = await client.SendAsync(AuthorizationMatrix.Upload(
            $"/api/v1/projects/{project.Key}/tasks/{task.Number}/attachments", "trigger-internal-error.txt", "x"), Ct);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await OpenApiContract.AssertResponseAsync(response, "UploadTaskAttachment");
        var text = await response.Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain("SECRET-INTERNAL-DETAIL", text, StringComparison.Ordinal);
        Assert.DoesNotContain("should-not-leak", text, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", text, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", text, StringComparison.Ordinal);
        var body = JsonDocument.Parse(text).RootElement;
        Assert.Equal("server.error", body.GetProperty("code").GetString());
        var traceId = body.GetProperty("traceId").GetString()!;
        // 運用者はログの追跡 ID から原因を調べられる
        Assert.Contains(factory.Logs, l => l.Contains(traceId, StringComparison.Ordinal) && l.Contains("SECRET-INTERNAL-DETAIL", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 添付のダウンロードは保存を強制し中身の種類を推測させない()
    {
        var owner = await factory.Data.CreateUserAsync();
        var project = await factory.Data.CreateProjectAsync(owner);
        var task = await factory.Data.CreateTaskAsync(project, owner);
        var attachment = await factory.Data.CreateAttachmentAsync(task, owner, "見積 もり<script>.txt", "<script>alert(1)</script>");
        using var client = await factory.CreateClientAsync(owner);

        using var response = await client.GetAsync(
            $"/api/v1/projects/{project.Key}/tasks/{task.Number}/attachments/{attachment.Id}/content", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var disposition = response.Content.Headers.ContentDisposition;
        Assert.NotNull(disposition);
        Assert.Equal("attachment", disposition.DispositionType);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal("<script>alert(1)</script>", Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync(Ct)));
    }

    [Theory]
    [InlineData("../../etc/passwd.txt", "passwd.txt")]
    [InlineData("..\\..\\windows\\boot.txt", "boot.txt")]
    [InlineData("/var/lib/taskmanager/attachments/x.txt", "x.txt")]
    public async Task アップロードのファイル名からパスを取り除く(string fileName, string stored)
    {
        var owner = await factory.Data.CreateUserAsync();
        var project = await factory.Data.CreateProjectAsync(owner);
        var task = await factory.Data.CreateTaskAsync(project, owner);
        using var client = await factory.CreateClientAsync(owner);

        using var response = await client.SendAsync(AuthorizationMatrix.Upload(
            $"/api/v1/projects/{project.Key}/tasks/{task.Number}/attachments", fileName, "中身"), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await OpenApiContract.AssertResponseAsync(response, "UploadTaskAttachment");
        var name = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("fileName").GetString();
        Assert.Equal(stored, name);
        Assert.All(Directory.EnumerateFiles(factory.AttachmentRoot, "*", SearchOption.AllDirectories),
            f => Assert.StartsWith(Path.GetFullPath(factory.AttachmentRoot), Path.GetFullPath(f), StringComparison.Ordinal));
    }

    [Fact]
    public async Task 許可されていない形式の添付は415()
    {
        var owner = await factory.Data.CreateUserAsync();
        var project = await factory.Data.CreateProjectAsync(owner);
        var task = await factory.Data.CreateTaskAsync(project, owner);
        using var client = await factory.CreateClientAsync(owner);

        using var response = await client.SendAsync(AuthorizationMatrix.Upload(
            $"/api/v1/projects/{project.Key}/tasks/{task.Number}/attachments", "page.html", "<html><script>alert(1)</script></html>"), Ct);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        await OpenApiContract.AssertResponseAsync(response, "UploadTaskAttachment");
    }

    [Fact]
    public async Task 要求の上限を超えるアップロードは413_attachment_too_large()
    {
        var owner = await factory.Data.CreateUserAsync();
        var project = await factory.Data.CreateProjectAsync(owner);
        var task = await factory.Data.CreateTaskAsync(project, owner);
        using var client = await factory.CreateClientAsync(owner);
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(new byte[12 * 1024 * 1024]);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", "large.txt");

        using var response = await client.PostAsync($"/api/v1/projects/{project.Key}/tasks/{task.Number}/attachments", form, Ct);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        await OpenApiContract.AssertResponseAsync(response, "UploadTaskAttachment");
        Assert.Equal("attachment.too_large", (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task ファイルなしのアップロードは400()
    {
        var owner = await factory.Data.CreateUserAsync();
        var project = await factory.Data.CreateProjectAsync(owner);
        var task = await factory.Data.CreateTaskAsync(project, owner);
        using var client = await factory.CreateClientAsync(owner);

        using var response = await client.PostAsync($"/api/v1/projects/{project.Key}/tasks/{task.Number}/attachments",
            new MultipartFormDataContent { { new StringContent("x"), "notfile" } }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ヘルスチェックはログインなしで応答し内部情報を含まない()
    {
        using var client = factory.CreateAnonymousClient();
        using var live = await client.GetAsync("/health/live", Ct);
        using var ready = await client.GetAsync("/health/ready", Ct);

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal("Healthy", await ready.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task システム情報は名前と版だけを返す()
    {
        using var client = factory.CreateAnonymousClient();
        using var response = await client.GetAsync("/api/v1/system/info", Ct);

        await OpenApiContract.AssertResponseAsync(response, "GetSystemInfo");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("TaskManager", body.GetProperty("name").GetString());
        Assert.Equal(["name", "version"], body.EnumerateObject().Select(p => p.Name).Order().ToArray());
    }
}
