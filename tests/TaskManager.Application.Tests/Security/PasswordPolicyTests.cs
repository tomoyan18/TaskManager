using TaskManager.Application.Security;
using TaskManager.Domain.Common;

namespace TaskManager.Application.Tests.Security;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("Kanagawa-Minato-2026")]
    [InlineData("12文字の日本語パスワードです")]
    public void Validate_条件を満たすパスワードは許可する(string password)
    {
        Assert.Null(PasswordPolicy.Validate(password, "owner@example.test", "テスト 管理者"));
    }

    [Fact]
    public void Validate_12文字ちょうどは許可し11文字は拒否する()
    {
        Assert.Null(PasswordPolicy.Validate("abcdefgh1234", null, null));
        Assert.Equal("パスワードは12文字以上で設定してください。", PasswordPolicy.Validate("abcdefgh123", null, null));
    }

    [Fact]
    public void Validate_128文字まで()
    {
        Assert.Null(PasswordPolicy.Validate(string.Concat(Enumerable.Repeat("ab1", 42)) + "xy", null, null));
        Assert.NotNull(PasswordPolicy.Validate(string.Concat(Enumerable.Repeat("ab1", 43)), null, null));
    }

    [Theory]
    [InlineData(" leading-space-1")]
    [InlineData("trailing-space1 ")]
    [InlineData("PASSWORD1234")]
    [InlineData("TaskManager2026")]
    [InlineData("xx-owner-pass-1")]
    [InlineData("テスト 管理者のパスワード")]
    [InlineData("abc-1111-defgh")]
    public void Validate_規則違反を拒否する(string password)
    {
        Assert.NotNull(PasswordPolicy.Validate(password, "owner@example.test", "テスト 管理者"));
    }

    [Fact]
    public void Validate_短いローカル部や表示名は含んでも拒否しない()
    {
        Assert.Null(PasswordPolicy.Validate("ab-is-short-ok", "ab@example.test", "ab"));
    }
}

public class ProjectPermissionRulesTests
{
    public static TheoryData<ProjectRole, ProjectPermission, bool> Matrix()
    {
        var data = new TheoryData<ProjectRole, ProjectPermission, bool>();
        var viewer = new[] { ProjectPermission.ViewProject, ProjectPermission.LeaveProject };
        var editor = viewer.Concat([
            ProjectPermission.CreateTasks, ProjectPermission.EditTasks, ProjectPermission.DeleteOwnTask, ProjectPermission.Comment,
            ProjectPermission.UploadAttachments, ProjectPermission.ManageTags, ProjectPermission.ManageMilestones]).ToArray();
        var owner = editor.Concat([
            ProjectPermission.DeleteAnyTask, ProjectPermission.DeleteAnyComment, ProjectPermission.DeleteAnyAttachment,
            ProjectPermission.ManageStatuses, ProjectPermission.EditProjectSettings, ProjectPermission.ArchiveProject,
            ProjectPermission.ManageMembers]).ToArray();
        foreach (var permission in Enum.GetValues<ProjectPermission>())
        {
            data.Add(ProjectRole.Viewer, permission, viewer.Contains(permission));
            data.Add(ProjectRole.Editor, permission, editor.Contains(permission));
            data.Add(ProjectRole.Owner, permission, owner.Contains(permission));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void IsGranted_ロールと権限の対応表どおり(ProjectRole role, ProjectPermission permission, bool expected)
    {
        Assert.Equal(expected, ProjectPermissionRules.IsGranted(role, permission));
    }

    [Theory]
    [InlineData(ProjectPermission.ViewProject, true)]
    [InlineData(ProjectPermission.ArchiveProject, true)]
    [InlineData(ProjectPermission.DeleteProject, true)]
    [InlineData(ProjectPermission.EditTasks, false)]
    [InlineData(ProjectPermission.Comment, false)]
    [InlineData(ProjectPermission.ManageMembers, false)]
    [InlineData(ProjectPermission.LeaveProject, false)]
    public void IsAllowedWhileArchived_参照とアーカイブ解除と削除だけ(ProjectPermission permission, bool expected)
    {
        Assert.Equal(expected, ProjectPermissionRules.IsAllowedWhileArchived(permission));
    }
}
