using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Tests.Support;
using TaskManager.Domain.Common;
using TaskManager.Testing;

namespace TaskManager.Api.Tests.Authorization;

/// <summary>
/// IDOR 専用テスト（testing-strategy.md §5）: プロジェクト A のメンバーが、URL は A のまま、
/// パスや本文に B の ID を指定しても 400 / 404 になり、B のデータが変化しないこと。
/// </summary>
public class IdorApiTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record Setup(
        HttpClient Client, TestProject A, int TaskA, TestProject B, Domain.Tasks.TaskItem TaskB, Guid StatusB, Guid TagB, Guid MilestoneB,
        Guid CommentB, Guid AttachmentB, TestUser OwnerB) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }

    private async Task<Setup> SetupAsync()
    {
        var data = factory.Data;
        var ownerA = await data.CreateUserAsync();
        var ownerB = await data.CreateUserAsync();
        var a = await data.CreateProjectAsync(ownerA);
        var b = await data.CreateProjectAsync(ownerB);
        var taskA = await data.CreateTaskAsync(a, ownerA);
        var taskB = await data.CreateTaskAsync(b, ownerB, "B の機密タスク");
        var statusB = await data.CreateStatusAsync(b, "B の状態");
        var tagB = await data.CreateTagAsync(b);
        var milestoneB = await data.CreateMilestoneAsync(b);
        var commentB = await data.CreateCommentAsync(taskB, ownerB, "B のコメント");
        var attachmentB = await data.CreateAttachmentAsync(taskB, ownerB, "secret.txt", "B の機密情報");
        return new Setup(await factory.CreateClientAsync(ownerA), a, taskA.Number, b, taskB, statusB.Id, tagB.Id, milestoneB.Id,
            commentB.Id, attachmentB.Id, ownerB);
    }

    [Fact]
    public async Task 子リソースのパスに他プロジェクトのIDを指定すると404()
    {
        using var s = await SetupAsync();
        var a = $"/api/v1/projects/{s.A.Key}";
        var requests = new (HttpMethod Method, string Url, object? Body)[]
        {
            (HttpMethod.Patch, $"{a}/statuses/{s.StatusB}", new { name = "乗っ取り" }),
            (HttpMethod.Delete, $"{a}/statuses/{s.StatusB}", null),
            (HttpMethod.Patch, $"{a}/tags/{s.TagB}", new { name = "乗っ取り" }),
            (HttpMethod.Delete, $"{a}/tags/{s.TagB}", null),
            (HttpMethod.Get, $"{a}/milestones/{s.MilestoneB}", null),
            (HttpMethod.Patch, $"{a}/milestones/{s.MilestoneB}", new { name = "乗っ取り", version = 0 }),
            (HttpMethod.Delete, $"{a}/milestones/{s.MilestoneB}", null),
            (HttpMethod.Patch, $"{a}/tasks/{s.TaskA}/comments/{s.CommentB}", new { body = "乗っ取り" }),
            (HttpMethod.Delete, $"{a}/tasks/{s.TaskA}/comments/{s.CommentB}", null),
            (HttpMethod.Get, $"{a}/tasks/{s.TaskA}/attachments/{s.AttachmentB}/content", null),
            (HttpMethod.Delete, $"{a}/tasks/{s.TaskA}/attachments/{s.AttachmentB}", null),
            (HttpMethod.Patch, $"{a}/members/{s.OwnerB.Id}", new { role = "viewer" }),
            (HttpMethod.Delete, $"{a}/members/{s.OwnerB.Id}", null),
            (HttpMethod.Get, $"/api/v1/projects/{s.B.Key}/tasks/{s.TaskB.Number}", null),
            (HttpMethod.Get, $"/api/v1/projects/{s.B.Key}/tasks/{s.TaskB.Number}/attachments/{s.AttachmentB}/content", null),
        };

        foreach (var (method, url, body) in requests)
        {
            using var request = new HttpRequestMessage(method, url) { Content = body is null ? null : JsonContent.Create(body) };
            using var response = await s.Client.SendAsync(request, Ct);
            Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"{method} {url}: {(int)response.StatusCode}");
        }

        await AssertBUnchangedAsync(s);
    }

    [Fact]
    public async Task 本文に他プロジェクトのIDを指定すると400()
    {
        using var s = await SetupAsync();
        var tasks = $"/api/v1/projects/{s.A.Key}/tasks";
        var bodies = new (object Body, string Field)[]
        {
            (new { title = "IDOR", statusId = s.StatusB }, "statusId"),
            (new { title = "IDOR", tagIds = new[] { s.TagB } }, "tagIds"),
            (new { title = "IDOR", milestoneId = s.MilestoneB }, "milestoneId"),
            (new { title = "IDOR", assigneeUserId = s.OwnerB.Id }, "assigneeUserId"),
        };

        foreach (var (body, field) in bodies)
        {
            using var create = await s.Client.PostAsJsonAsync(tasks, body, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
            Assert.True((await create.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Ct)).GetProperty("errors").TryGetProperty(field, out _), field);
        }

        using var update = await s.Client.PatchAsJsonAsync($"{tasks}/{s.TaskA}", new { statusId = s.StatusB, version = 0 }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
        // 使用中のステータスを削除するとき、移動先に B のステータスは指定できない
        await factory.Data.CreateTaskAsync(s.A, s.OwnerB, "中止のタスク", category: StatusCategory.Canceled);
        var canceledA = await factory.Data.GetStatusAsync(s.A, StatusCategory.Canceled);
        using var deleteStatus = await s.Client.DeleteAsync(
            $"/api/v1/projects/{s.A.Key}/statuses/{canceledA.Id}?moveTasksToStatusId={s.StatusB}", Ct);
        Assert.Equal(HttpStatusCode.BadRequest, deleteStatus.StatusCode);
        Assert.True(await factory.Data.QueryAsync(db => db.WorkflowStatuses.AnyAsync(x => x.Id == canceledA.Id, Ct)));
        using var reorder = await s.Client.PutAsJsonAsync($"/api/v1/projects/{s.A.Key}/statuses/order", new { statusIds = new[] { s.StatusB } }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, reorder.StatusCode);

        await AssertBUnchangedAsync(s);
    }

    [Fact]
    public async Task 他人宛てのお知らせは既読にできない()
    {
        using var s = await SetupAsync();
        var noticeId = await factory.Data.QueryAsync(async db =>
        {
            var n = Domain.Notices.Notice.Create(s.OwnerB.Id, NoticeKind.TaskAssigned, null, s.B.Id, s.TaskB.Id, "B 宛て", "/projects", DateTime.UtcNow);
            db.Notices.Add(n);
            await db.SaveChangesAsync(Ct);
            return n.Id;
        });

        using var response = await s.Client.PostAsync($"/api/v1/me/notices/{noticeId}/read", null, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(await factory.Data.QueryAsync(db => db.Notices.Where(n => n.Id == noticeId).Select(n => n.ReadAtUtc).SingleAsync(Ct)));
    }

    [Fact]
    public async Task 一般利用者は利用者管理APIで他人の情報を見られない()
    {
        using var s = await SetupAsync();
        using var response = await s.Client.GetAsync($"/api/v1/users/{s.OwnerB.Id}", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task AssertBUnchangedAsync(Setup s)
    {
        Assert.True(await factory.Data.QueryAsync(db => db.WorkflowStatuses.AnyAsync(x => x.Id == s.StatusB && x.Name == "B の状態", Ct)));
        Assert.True(await factory.Data.QueryAsync(db => db.Tags.AnyAsync(x => x.Id == s.TagB, Ct)));
        Assert.True(await factory.Data.QueryAsync(db => db.Milestones.AnyAsync(x => x.Id == s.MilestoneB && x.RowVersion == 0, Ct)));
        Assert.True(await factory.Data.QueryAsync(db => db.TaskComments.AnyAsync(x => x.Id == s.CommentB && x.Body == "B のコメント", Ct)));
        Assert.True(await factory.Data.QueryAsync(db => db.TaskAttachments.AnyAsync(x => x.Id == s.AttachmentB, Ct)));
        Assert.True(await factory.Data.QueryAsync(db => db.ProjectMemberships.AnyAsync(
            x => x.ProjectId == s.B.Id && x.UserId == s.OwnerB.Id && x.Role == ProjectRole.Owner, Ct)));
        Assert.False(await factory.Data.QueryAsync(db => db.TaskItems.AnyAsync(t => t.ProjectId == s.A.Id && t.Title == "IDOR", Ct)));
    }
}
