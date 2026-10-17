using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Common;
using TaskManager.Application.Tasks;
using TaskManager.Domain.Common;
using TaskManager.Testing;

namespace TaskManager.Application.Tests.Tasks;

public class TaskCommentServiceTests(DatabaseFixture fixture) : ApplicationTestBase(fixture)
{
    private Task<TaskCommentResponse> CommentAsync(TestUser actor, TestProject project, int number, string body) =>
        Host.RunAsync<TaskCommentService, TaskCommentResponse>(actor, s => s.CreateAsync(
            project.Key, number, new CreateTaskCommentRequest { Body = body }, Ct));

    [Fact]
    public async Task Create_担当者と作成者にお知らせし投稿者本人には送らない()
    {
        var owner = await Data.CreateUserAsync();
        var assignee = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        await Data.AddMemberAsync(project, assignee, ProjectRole.Editor);
        var task = await Data.CreateTaskAsync(project, owner, assigneeUserId: assignee.Id);

        var comment = await CommentAsync(assignee, project, task.Number, "単価表を **共有** します。");

        Assert.True(comment.CanEdit && comment.CanDelete);
        Assert.Equal(NoticeKind.TaskCommented,
            (await Data.QueryAsync(db => db.Notices.SingleAsync(n => n.RecipientUserId == owner.Id, Ct))).Kind);
        Assert.False(await Data.QueryAsync(db => db.Notices.AnyAsync(n => n.RecipientUserId == assignee.Id, Ct)));
    }

    [Fact]
    public async Task Create_5分以内の未読のお知らせはまとめる()
    {
        var owner = await Data.CreateUserAsync();
        var editor = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        await Data.AddMemberAsync(project, editor, ProjectRole.Editor);
        var task = await Data.CreateTaskAsync(project, owner);

        await CommentAsync(editor, project, task.Number, "1件目");
        Host.Time.Advance(TimeSpan.FromMinutes(4));
        await CommentAsync(editor, project, task.Number, "2件目");
        Assert.Equal(1, await Data.QueryAsync(db => db.Notices.CountAsync(n => n.RecipientUserId == owner.Id, Ct)));

        Host.Time.Advance(TimeSpan.FromMinutes(6));
        await CommentAsync(editor, project, task.Number, "3件目");
        Assert.Equal(2, await Data.QueryAsync(db => db.Notices.CountAsync(n => n.RecipientUserId == owner.Id, Ct)));
    }

    [Fact]
    public async Task Create_閲覧者はコメントできない()
    {
        var owner = await Data.CreateUserAsync();
        var viewer = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        await Data.AddMemberAsync(project, viewer, ProjectRole.Viewer);
        var task = await Data.CreateTaskAsync(project, owner);

        await AppAssert.ForbiddenAsync(() => CommentAsync(viewer, project, task.Number, "閲覧者のコメント"));
    }

    [Fact]
    public async Task Create_空白だけのコメントは項目エラー()
    {
        var owner = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        var task = await Data.CreateTaskAsync(project, owner);
        await AppAssert.ValidationAsync("body", () => CommentAsync(owner, project, task.Number, "  \n "));
    }

    [Fact]
    public async Task Update_投稿者以外は編集できず管理者は削除できる()
    {
        var owner = await Data.CreateUserAsync();
        var editor = await Data.CreateUserAsync();
        var other = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        await Data.AddMemberAsync(project, editor, ProjectRole.Editor);
        await Data.AddMemberAsync(project, other, ProjectRole.Editor);
        var task = await Data.CreateTaskAsync(project, owner);
        var comment = await CommentAsync(editor, project, task.Number, "編集者のコメント");

        await AppAssert.ForbiddenAsync(() => Host.RunAsync<TaskCommentService, TaskCommentResponse>(owner, s => s.UpdateAsync(
            project.Key, task.Number, comment.Id, new UpdateTaskCommentRequest { Body = "管理者による書き換え" }, Ct)));
        await AppAssert.ForbiddenAsync(() => Host.RunAsync<TaskCommentService>(other, s => s.DeleteAsync(project.Key, task.Number, comment.Id, Ct)));

        var edited = await Host.RunAsync<TaskCommentService, TaskCommentResponse>(editor, s => s.UpdateAsync(
            project.Key, task.Number, comment.Id, new UpdateTaskCommentRequest { Body = "修正しました" }, Ct));
        Assert.NotNull(edited.EditedAtUtc);

        await Host.RunAsync<TaskCommentService>(owner, s => s.DeleteAsync(project.Key, task.Number, comment.Id, Ct));
        Assert.False(await Data.QueryAsync(db => db.TaskComments.AnyAsync(c => c.Id == comment.Id, Ct)));
    }

    [Fact]
    public async Task Delete_別タスクのコメントIDは404()
    {
        var owner = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        var task1 = await Data.CreateTaskAsync(project, owner);
        var task2 = await Data.CreateTaskAsync(project, owner);
        var comment = await CommentAsync(owner, project, task1.Number, "タスク1のコメント");

        await AppAssert.NotFoundAsync(() => Host.RunAsync<TaskCommentService>(owner, s => s.DeleteAsync(project.Key, task2.Number, comment.Id, Ct)));
        Assert.True(await Data.QueryAsync(db => db.TaskComments.AnyAsync(c => c.Id == comment.Id, Ct)));
    }

    [Fact]
    public async Task List_本文はMarkdownのまま保存し返す()
    {
        var owner = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        var task = await Data.CreateTaskAsync(project, owner);
        const string body = "<script>alert(1)</script> [link](javascript:alert(1))";
        await CommentAsync(owner, project, task.Number, body);

        var list = await Host.RunAsync<TaskCommentService, PagedResponse<TaskCommentResponse>>(
            owner, s => s.ListAsync(project.Key, task.Number, null, null, Ct));

        // 無害化は表示側（Frontend の Markdown 描画）で行う。API は入力をそのまま返す
        Assert.Equal(body, Assert.Single(list.Items).Body);
    }
}

public class TaskAttachmentServiceTests(DatabaseFixture fixture) : ApplicationTestBase(fixture)
{
    private static readonly byte[] PdfBytes = [.. "%PDF-1.7\n"u8, .. new byte[100]];

    private async Task<(TestUser Owner, TestProject Project, int Number)> NewTaskAsync()
    {
        var owner = await Data.CreateUserAsync();
        var project = await Data.CreateProjectAsync(owner);
        var task = await Data.CreateTaskAsync(project, owner);
        return (owner, project, task.Number);
    }

    private Task<TaskAttachmentResponse> UploadAsync(TestUser actor, TestProject project, int number, string fileName, byte[] content, long? length = null)
    {
        var stream = new MemoryStream(content);
        return Host.RunAsync<TaskAttachmentService, TaskAttachmentResponse>(actor, async s =>
        {
            await using (stream)
            {
                return await s.UploadAsync(project.Key, number, fileName, stream, length ?? content.LongLength, Ct);
            }
        });
    }

    [Fact]
    public async Task Upload_中身を確認して保存しダウンロードできる()
    {
        var (owner, project, number) = await NewTaskAsync();

        var uploaded = await UploadAsync(owner, project, number, "../../見積書.pdf", PdfBytes);

        Assert.Equal("見積書.pdf", uploaded.FileName);
        Assert.Equal("application/pdf", uploaded.ContentType);
        Assert.Equal(PdfBytes.Length, uploaded.SizeBytes);
        var content = await Host.RunAsync<TaskAttachmentService, AttachmentContent>(owner, s => s.OpenReadAsync(project.Key, number, uploaded.Id, Ct));
        await using (content.Content)
        {
            using var copy = new MemoryStream();
            await content.Content.CopyToAsync(copy, Ct);
            Assert.Equal(PdfBytes, copy.ToArray());
        }

        // 保存先は添付ディレクトリの中だけ（元のファイル名をパスに使わない）
        var files = Directory.EnumerateFiles(Host.AttachmentRoot, "*", SearchOption.AllDirectories).ToList();
        var stored = Assert.Single(files);
        Assert.DoesNotContain("見積書", stored, StringComparison.Ordinal);
        Assert.Contains(TaskHistoryKind.AttachmentAdded, await Data.QueryAsync(db => db.TaskHistoryEntries
            .Where(h => h.Kind == TaskHistoryKind.AttachmentAdded && h.NewValue == "見積書.pdf").Select(h => h.Kind).ToListAsync(Ct)));
    }

    [Theory]
    [InlineData("report.pdf", "PNGではない")]
    [InlineData("image.png", "%PDF-1.7")]
    [InlineData("script.exe", "MZ")]
    [InlineData("page.html", "<html>")]
    [InlineData("noextension", "text")]
    public async Task Upload_拡張子と中身が一致しない_許可外の形式は415(string fileName, string text)
    {
        var (owner, project, number) = await NewTaskAsync();
        await AppAssert.ThrowsAsync(AppErrorKind.UnsupportedMediaType, ErrorCodes.AttachmentTypeNotAllowed,
            () => UploadAsync(owner, project, number, fileName, System.Text.Encoding.UTF8.GetBytes(text)));
        Assert.False(Directory.Exists(Host.AttachmentRoot) && Directory.EnumerateFiles(Host.AttachmentRoot, "*", SearchOption.AllDirectories).Any());
    }

    [Fact]
    public async Task Upload_テキストにNULが含まれると拒否する()
    {
        var (owner, project, number) = await NewTaskAsync();
        await AppAssert.ThrowsAsync(AppErrorKind.UnsupportedMediaType, ErrorCodes.AttachmentTypeNotAllowed,
            () => UploadAsync(owner, project, number, "memo.txt", [0x41, 0x00, 0x42]));
    }

    [Fact]
    public async Task Upload_10MBちょうどは許可し超過は413()
    {
        var (owner, project, number) = await NewTaskAsync();
        var exact = new byte[10 * 1024 * 1024];
        "%PDF-"u8.CopyTo(exact);

        await UploadAsync(owner, project, number, "exact.pdf", exact);
        await AppAssert.ThrowsAsync(AppErrorKind.PayloadTooLarge, ErrorCodes.AttachmentTooLarge,
            () => UploadAsync(owner, project, number, "over.pdf", PdfBytes, length: exact.Length + 1L));
    }

    [Fact]
    public async Task Upload_空のファイルは項目エラー()
    {
        var (owner, project, number) = await NewTaskAsync();
        await AppAssert.ValidationAsync("file", () => UploadAsync(owner, project, number, "empty.txt", []));
    }

    [Fact]
    public async Task Upload_1タスク20件まで()
    {
        var (owner, project, number) = await NewTaskAsync();
        for (var i = 0; i < 20; i++)
        {
            await UploadAsync(owner, project, number, $"memo{i}.txt", "メモ"u8.ToArray());
        }

        await AppAssert.ThrowsAsync(AppErrorKind.Conflict, ErrorCodes.AttachmentLimitReached,
            () => UploadAsync(owner, project, number, "memo21.txt", "メモ"u8.ToArray()));
    }

    [Fact]
    public async Task Delete_他の編集者のファイルは削除できず投稿者は削除できる()
    {
        var (owner, project, number) = await NewTaskAsync();
        var editor = await Data.CreateUserAsync();
        var other = await Data.CreateUserAsync();
        await Data.AddMemberAsync(project, editor, ProjectRole.Editor);
        await Data.AddMemberAsync(project, other, ProjectRole.Editor);
        var uploaded = await UploadAsync(editor, project, number, "memo.txt", "メモ"u8.ToArray());

        await AppAssert.ForbiddenAsync(() => Host.RunAsync<TaskAttachmentService>(other, s => s.DeleteAsync(project.Key, number, uploaded.Id, Ct)));
        await Host.RunAsync<TaskAttachmentService>(editor, s => s.DeleteAsync(project.Key, number, uploaded.Id, Ct));

        Assert.Empty(Directory.EnumerateFiles(Host.AttachmentRoot, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task OpenRead_別プロジェクトの添付IDは404()
    {
        var (owner, project, number) = await NewTaskAsync();
        var otherOwner = await Data.CreateUserAsync();
        var other = await Data.CreateProjectAsync(otherOwner);
        var otherTask = await Data.CreateTaskAsync(other, otherOwner);
        var secret = await UploadAsync(otherOwner, other, otherTask.Number, "secret.txt", "社外秘"u8.ToArray());

        await AppAssert.NotFoundAsync(() => Host.RunAsync<TaskAttachmentService, AttachmentContent>(
            owner, s => s.OpenReadAsync(project.Key, number, secret.Id, Ct)));
        await AppAssert.NotFoundAsync(() => Host.RunAsync<TaskAttachmentService, AttachmentContent>(
            owner, s => s.OpenReadAsync(other.Key, otherTask.Number, secret.Id, Ct)));
    }
}
