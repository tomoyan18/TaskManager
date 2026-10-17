using TaskManager.Domain.Common;
using TaskManager.Domain.Notices;
using TaskManager.Domain.Tasks;

namespace TaskManager.Domain.Tests.Tasks;

public class TaskAttachmentFileNameTests
{
    [Theory]
    [InlineData("見積書.pdf", "見積書.pdf")]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("..\\..\\windows\\win.ini", "win.ini")]
    [InlineData("C:\\Users\\a\\report.xlsx", "report.xlsx")]
    [InlineData(".htaccess", "htaccess")]
    [InlineData("a\u0000b\u001F.txt", "ab.txt")]
    [InlineData("in<va>lid:na*me?\"|.csv", "invalidname.csv")]
    [InlineData("", "file")]
    [InlineData("../", "file")]
    [InlineData(null, "file")]
    public void NormalizeFileName_パス区切りと使えない文字を除く(string? input, string expected)
    {
        Assert.Equal(expected, TaskAttachment.NormalizeFileName(input));
    }

    [Fact]
    public void NormalizeFileName_長い名前は拡張子を残して255文字に収める()
    {
        var name = TaskAttachment.NormalizeFileName(new string('a', 300) + ".pdf");
        Assert.Equal(255, name.Length);
        Assert.EndsWith(".pdf", name, StringComparison.Ordinal);
    }
}

public class TaskCommentTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_空のコメントは拒否する()
    {
        var ex = Assert.Throws<DomainException>(() => TaskComment.Create(Guid.NewGuid(), Guid.NewGuid(), " \n ", Now));
        Assert.Equal("body", ex.Field);
    }

    [Fact]
    public void Edit_本文と編集日時を更新する()
    {
        var comment = TaskComment.Create(Guid.NewGuid(), Guid.NewGuid(), "最初", Now);
        Assert.Null(comment.EditedAtUtc);
        comment.Edit("修正後", Now.AddMinutes(1));
        Assert.Equal("修正後", comment.Body);
        Assert.Equal(Now.AddMinutes(1), comment.EditedAtUtc);
    }

    [Fact]
    public void Create_10000文字まで()
    {
        TaskComment.Create(Guid.NewGuid(), Guid.NewGuid(), new string('a', TaskComment.BodyMaxLength), Now);
        Assert.Throws<DomainException>(() =>
            TaskComment.Create(Guid.NewGuid(), Guid.NewGuid(), new string('a', TaskComment.BodyMaxLength + 1), Now));
    }
}

public class NoticeTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("https://evil.example.test/")]
    [InlineData("//evil.example.test/")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/\\evil.example.test/")]
    [InlineData("/\t/evil.example.test/")]
    [InlineData("/projects/A B")]
    public void Create_リンクは画面内の相対パスだけ(string linkPath)
    {
        Assert.Throws<ArgumentException>(() =>
            Notice.Create(Guid.NewGuid(), NoticeKind.TaskAssigned, null, null, null, "本文", linkPath, Now));
    }

    [Fact]
    public void Create_長い本文は300文字に切り詰める()
    {
        var notice = Notice.Create(Guid.NewGuid(), NoticeKind.TaskCommented, null, null, null, new string('a', 400), "/projects/SALES/tasks/1", Now);
        Assert.Equal(Notice.MessageMaxLength, notice.Message.Length);
        Assert.EndsWith("…", notice.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MarkRead_最初の既読日時を保つ()
    {
        var notice = Notice.Create(Guid.NewGuid(), NoticeKind.TaskAssigned, null, null, null, "本文", null, Now);
        Assert.False(notice.IsRead);
        notice.MarkRead(Now.AddMinutes(1));
        notice.MarkRead(Now.AddMinutes(2));
        Assert.Equal(Now.AddMinutes(1), notice.ReadAtUtc);
    }

    [Fact]
    public void Refresh_本文と日時を更新する()
    {
        var notice = Notice.Create(Guid.NewGuid(), NoticeKind.TaskCommented, null, null, null, "1件目", null, Now);
        var actor = Guid.NewGuid();
        notice.Refresh(actor, "2件目", Now.AddMinutes(3));
        Assert.Equal("2件目", notice.Message);
        Assert.Equal(actor, notice.ActorUserId);
        Assert.Equal(Now.AddMinutes(3), notice.CreatedAtUtc);
    }
}

public class TaskHistoryEntryTests
{
    [Fact]
    public void Create_長い値は500文字に切り詰める()
    {
        var entry = TaskHistoryEntry.Create(Guid.NewGuid(), TaskHistoryKind.TitleChanged, null, new string('a', 600), "b",
            new DateTime(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal(TaskHistoryEntry.ValueMaxLength, entry.OldValue!.Length);
        Assert.Equal("b", entry.NewValue);
        Assert.Null(entry.ActorUserId);
    }
}
