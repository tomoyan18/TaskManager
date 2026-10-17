using TaskManager.Domain.Common;
using TaskManager.Domain.Projects;

namespace TaskManager.Domain.Tests.Projects;

public class ProjectTests
{
    [Fact]
    public void CreateDefaultStatuses_既定のステータス5件を作る()
    {
        var project = Project.Create(ProjectKey.Create("sales"), "営業支援", "  ");
        Assert.Equal("SALES", project.Key);
        Assert.Null(project.Description);

        var statuses = project.CreateDefaultStatuses();
        Assert.Equal(["未着手", "対応中", "確認待ち", "完了", "中止"], statuses.Select(s => s.Name).ToArray());
        Assert.Equal(
            [StatusCategory.NotStarted, StatusCategory.InProgress, StatusCategory.InProgress, StatusCategory.Done, StatusCategory.Canceled],
            statuses.Select(s => s.Category).ToArray());
        Assert.Single(statuses, s => s.IsDefault);
        Assert.True(statuses[0].IsDefault);
        Assert.All(statuses, s => Assert.Equal(project.Id, s.ProjectId));
        Assert.Equal([1, 2, 3, 4, 5], statuses.Select(s => (int)s.SortOrder).ToArray());
    }

    [Fact]
    public void Archive_最初のアーカイブ日時を保ち解除できる()
    {
        var project = Project.Create(ProjectKey.Create("SALES"), "営業支援", null);
        var t1 = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        project.Archive(t1);
        project.Archive(t1.AddDays(1));
        Assert.True(project.IsArchived);
        Assert.Equal(t1, project.ArchivedAtUtc);
        project.Unarchive();
        Assert.False(project.IsArchived);
    }

    [Fact]
    public void Rename_80文字まで()
    {
        var project = Project.Create(ProjectKey.Create("SALES"), "営業支援", null);
        project.Rename(new string('名', 80));
        var ex = Assert.Throws<DomainException>(() => project.Rename(new string('名', 81)));
        Assert.Equal("name", ex.Field);
    }
}

public class WorkflowStatusTests
{
    private static readonly Guid ProjectId = Guid.CreateVersion7();

    [Theory]
    [InlineData(StatusCategory.NotStarted, true)]
    [InlineData(StatusCategory.InProgress, true)]
    [InlineData(StatusCategory.Done, false)]
    [InlineData(StatusCategory.Canceled, false)]
    public void CanBeDefault_未着手と進行中だけ(StatusCategory category, bool expected)
    {
        Assert.Equal(expected, WorkflowStatus.CanBeDefault(category));
    }

    [Fact]
    public void Create_完了区分を既定にすると拒否する()
    {
        var ex = Assert.Throws<DomainException>(() =>
            WorkflowStatus.Create(ProjectId, "完了", StatusCategory.Done, StatusColor.Green, 1, isDefault: true));
        Assert.Equal("isDefault", ex.Field);
    }

    [Fact]
    public void ChangeCategory_既定のステータスを終了区分にできない()
    {
        var status = WorkflowStatus.Create(ProjectId, "未着手", StatusCategory.NotStarted, StatusColor.Gray, 1, isDefault: true);
        var ex = Assert.Throws<DomainException>(() => status.ChangeCategory(StatusCategory.Canceled));
        Assert.Equal("category", ex.Field);
        status.ChangeCategory(StatusCategory.InProgress);
        Assert.Equal(StatusCategory.InProgress, status.Category);
    }

    [Fact]
    public void ChangeCategory_既定でなければ終了区分にできる()
    {
        var status = WorkflowStatus.Create(ProjectId, "保留", StatusCategory.InProgress, StatusColor.Yellow, 2, isDefault: false);
        status.ChangeCategory(StatusCategory.Canceled);
        Assert.Equal(StatusCategory.Canceled, status.Category);
    }

    [Fact]
    public void Create_名前は30文字まで()
    {
        WorkflowStatus.Create(ProjectId, new string('a', 30), StatusCategory.InProgress, StatusColor.Blue, 1, false);
        Assert.Throws<DomainException>(() =>
            WorkflowStatus.Create(ProjectId, new string('a', 31), StatusCategory.InProgress, StatusColor.Blue, 1, false));
    }

    [Theory]
    [InlineData(ProjectRole.Viewer, false)]
    [InlineData(ProjectRole.Editor, true)]
    [InlineData(ProjectRole.Owner, true)]
    public void CanBeAssigned_編集者以上(ProjectRole role, bool expected)
    {
        Assert.Equal(expected, role.CanBeAssigned());
    }
}
