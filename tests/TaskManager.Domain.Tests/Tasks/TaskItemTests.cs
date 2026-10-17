using TaskManager.Domain.Common;
using TaskManager.Domain.Projects;
using TaskManager.Domain.Tasks;

namespace TaskManager.Domain.Tests.Tasks;

public class TaskItemTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 1, 0, 0, DateTimeKind.Utc);

    private readonly Project _project = Project.Create(ProjectKey.Create("SALES"), "営業支援", null);
    private readonly Dictionary<StatusCategory, WorkflowStatus> _statuses;

    public TaskItemTests()
    {
        _statuses = _project.CreateDefaultStatuses()
            .GroupBy(s => s.Category).ToDictionary(g => g.Key, g => g.First());
    }

    private TaskItem NewTask(int number = 1, string title = "見積書の作成") =>
        TaskItem.Create(_project.Id, number, title, _statuses[StatusCategory.NotStarted], TaskPriority.Normal, Now);

    [Fact]
    public void Create_タイトルを正規化し未完了で作る()
    {
        var task = NewTask(title: "  見積書の作成  ");
        Assert.Equal("見積書の作成", task.Title);
        Assert.Equal(_project.Id, task.ProjectId);
        Assert.Equal(1, task.Number);
        Assert.Null(task.CompletedAtUtc);
        Assert.NotEqual(Guid.Empty, task.Id);
    }

    [Fact]
    public void Create_番号は1以上()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewTask(number: 0));
    }

    [Fact]
    public void Create_タイトル200文字は許可し201文字は拒否する()
    {
        Assert.Equal(200, NewTask(title: new string('a', 200)).Title.Length);
        var ex = Assert.Throws<DomainException>(() => NewTask(title: new string('a', 201)));
        Assert.Equal("title", ex.Field);
    }

    // TK-05: 完了日時
    [Fact]
    public void ChangeStatus_完了区分へ変えると完了日時を設定する()
    {
        var task = NewTask();
        task.ChangeStatus(_statuses[StatusCategory.Done], StatusCategory.NotStarted, Now.AddHours(1));
        Assert.Equal(Now.AddHours(1), task.CompletedAtUtc);
    }

    [Fact]
    public void ChangeStatus_完了から完了への変更では完了日時を保つ()
    {
        var task = NewTask();
        task.ChangeStatus(_statuses[StatusCategory.Done], StatusCategory.NotStarted, Now.AddHours(1));
        var anotherDone = WorkflowStatus.Create(_project.Id, "納品済み", StatusCategory.Done, StatusColor.Teal, 6, isDefault: false);
        task.ChangeStatus(anotherDone, StatusCategory.Done, Now.AddHours(5));
        Assert.Equal(Now.AddHours(1), task.CompletedAtUtc);
    }

    [Theory]
    [InlineData(StatusCategory.InProgress)]
    [InlineData(StatusCategory.Canceled)]
    [InlineData(StatusCategory.NotStarted)]
    public void ChangeStatus_完了以外へ戻すと完了日時を消す(StatusCategory next)
    {
        var task = NewTask();
        task.ChangeStatus(_statuses[StatusCategory.Done], StatusCategory.NotStarted, Now);
        task.ChangeStatus(_statuses[next], StatusCategory.Done, Now.AddHours(1));
        Assert.Null(task.CompletedAtUtc);
    }

    [Fact]
    public void ChangeStatus_別プロジェクトのステータスは拒否する()
    {
        var other = Project.Create(ProjectKey.Create("OTHER"), "別", null);
        var foreign = other.CreateDefaultStatuses()[0];
        var ex = Assert.Throws<DomainException>(() => NewTask().ChangeStatus(foreign, StatusCategory.NotStarted, Now));
        Assert.Equal("statusId", ex.Field);
    }

    // TK-02: 日付
    [Fact]
    public void ChangeDates_開始日と期限が同じ日は許可する()
    {
        var task = NewTask();
        var day = new DateOnly(2026, 10, 9);
        task.ChangeDates(day, day);
        Assert.Equal(day, task.StartDate);
        Assert.Equal(day, task.DueDate);
    }

    [Fact]
    public void ChangeDates_期限が開始日より前なら拒否する()
    {
        var ex = Assert.Throws<DomainException>(() =>
            NewTask().ChangeDates(new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 9)));
        Assert.Equal("dueDate", ex.Field);
        Assert.Equal("期限は開始日以降の日付にしてください。", ex.Message);
    }

    [Theory]
    [InlineData(1999, 12, 31, "startDate")]
    [InlineData(2100, 1, 1, "startDate")]
    public void ChangeDates_範囲外の開始日を拒否する(int y, int m, int d, string field)
    {
        var ex = Assert.Throws<DomainException>(() => NewTask().ChangeDates(new DateOnly(y, m, d), null));
        Assert.Equal(field, ex.Field);
    }

    [Fact]
    public void ChangeDates_範囲の両端は許可する()
    {
        var task = NewTask();
        task.ChangeDates(TaskItem.MinDate, TaskItem.MaxDate);
        Assert.Equal(TaskItem.MaxDate, task.DueDate);
        Assert.Throws<DomainException>(() => task.ChangeDates(null, TaskItem.MaxDate.AddDays(1)));
    }

    // TK-03: 親子は 1 段のみ
    [Fact]
    public void ChangeParent_通常のタスクを親にできる()
    {
        var parent = NewTask(1);
        var child = NewTask(2);
        child.ChangeParent(parent, hasSubtasks: false);
        Assert.Equal(parent.Id, child.ParentTaskId);
        child.ChangeParent(null, hasSubtasks: false);
        Assert.Null(child.ParentTaskId);
    }

    [Fact]
    public void ChangeParent_自分自身は拒否する()
    {
        var task = NewTask();
        var ex = Assert.Throws<DomainException>(() => task.ChangeParent(task, hasSubtasks: false));
        Assert.Equal("parentTaskNumber", ex.Field);
    }

    [Fact]
    public void ChangeParent_サブタスクの下にはサブタスクを作れない()
    {
        var root = NewTask(1);
        var child = NewTask(2);
        child.ChangeParent(root, hasSubtasks: false);
        var grandChild = NewTask(3);
        Assert.Throws<DomainException>(() => grandChild.ChangeParent(child, hasSubtasks: false));
    }

    [Fact]
    public void ChangeParent_サブタスクを持つタスクは別タスクの子にできない()
    {
        var parent = NewTask(1);
        var task = NewTask(2);
        Assert.Throws<DomainException>(() => task.ChangeParent(parent, hasSubtasks: true));
    }

    [Fact]
    public void ChangeParent_別プロジェクトのタスクは拒否する()
    {
        var other = Project.Create(ProjectKey.Create("OTHER"), "別", null);
        var foreign = TaskItem.Create(other.Id, 1, "別のタスク", other.CreateDefaultStatuses()[0], TaskPriority.Low, Now);
        Assert.Throws<DomainException>(() => NewTask().ChangeParent(foreign, hasSubtasks: false));
    }

    // TK-06: タグ
    [Fact]
    public void ReplaceTags_置き換えと変更有無を返す()
    {
        var task = NewTask();
        var a = Tag.Create(_project.Id, "見積", StatusColor.Blue);
        var b = Tag.Create(_project.Id, "至急対応", StatusColor.Red);
        Assert.True(task.ReplaceTags([a, b]));
        Assert.False(task.ReplaceTags([b, a]));
        Assert.True(task.ReplaceTags([b]));
        Assert.Equal(new[] { b.Id }, task.Tags.Select(t => t.TagId));
        Assert.True(task.ReplaceTags([]));
        Assert.Empty(task.Tags);
    }

    [Fact]
    public void ReplaceTags_10個まで許可し11個は拒否する()
    {
        var task = NewTask();
        var tags = Enumerable.Range(1, 11).Select(i => Tag.Create(_project.Id, $"タグ{i}", StatusColor.Gray)).ToList();
        Assert.True(task.ReplaceTags(tags.Take(10).ToList()));
        var ex = Assert.Throws<DomainException>(() => task.ReplaceTags(tags));
        Assert.Equal("tagIds", ex.Field);
    }

    [Fact]
    public void ReplaceTags_別プロジェクトのタグは拒否する()
    {
        var other = Project.Create(ProjectKey.Create("OTHER"), "別", null);
        Assert.Throws<DomainException>(() => NewTask().ReplaceTags([Tag.Create(other.Id, "外部", StatusColor.Gray)]));
    }

    [Fact]
    public void ChangeMilestone_別プロジェクトのマイルストーンは拒否する()
    {
        var other = Project.Create(ProjectKey.Create("OTHER"), "別", null);
        var task = NewTask();
        Assert.Throws<DomainException>(() => task.ChangeMilestone(Milestone.Create(other.Id, "外部", null, null)));
        var own = Milestone.Create(_project.Id, "10月リリース", null, null);
        task.ChangeMilestone(own);
        Assert.Equal(own.Id, task.MilestoneId);
        task.ChangeMilestone(null);
        Assert.Null(task.MilestoneId);
    }

    [Theory]
    [InlineData("2026-10-08", StatusCategory.InProgress, true)]
    [InlineData("2026-10-09", StatusCategory.InProgress, false)]
    [InlineData("2026-10-08", StatusCategory.Done, false)]
    [InlineData("2026-10-08", StatusCategory.Canceled, false)]
    [InlineData(null, StatusCategory.NotStarted, false)]
    public void IsOverdue_終了区分でなく期限が今日より前の場合だけtrue(string? due, StatusCategory category, bool expected)
    {
        DateOnly? dueDate = due is null ? null : DateOnly.Parse(due, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(expected, TaskItem.IsOverdue(dueDate, category, new DateOnly(2026, 10, 9)));
    }

    [Fact]
    public void ChangeDescription_空白だけならnull()
    {
        var task = NewTask();
        task.ChangeDescription("  \n ");
        Assert.Null(task.Description);
        Assert.Throws<DomainException>(() => task.ChangeDescription(new string('a', TaskItem.DescriptionMaxLength + 1)));
    }
}
