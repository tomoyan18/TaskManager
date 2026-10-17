using Microsoft.Extensions.DependencyInjection;
using TaskManager.Api.Tests.Support;
using TaskManager.Domain.Common;
using TaskManager.Domain.Notices;
using TaskManager.Infrastructure.Persistence;
using TaskManager.Testing;

namespace TaskManager.Api.Tests.Authorization;

/// <summary>
/// 認可マトリクスの 1 ケース分のデータ。ケースごとに新しく作り、他のケースの操作（削除・変更）の影響を受けないようにする。
/// </summary>
public sealed class AuthorizationWorld
{
    private AuthorizationWorld()
    {
    }

    public TestUser Owner { get; private init; } = null!;

    public TestUser Editor { get; private init; } = null!;

    public TestUser Viewer { get; private init; } = null!;

    public TestUser NonMember { get; private init; } = null!;

    public TestUser Administrator { get; private init; } = null!;

    public TestUser TemporaryPasswordUser { get; private init; } = null!;

    /// <summary>ロール変更・削除の対象（閲覧者）。</summary>
    public TestUser MemberTarget { get; private init; } = null!;

    /// <summary>メンバー追加の対象（未所属）。</summary>
    public TestUser Candidate { get; private init; } = null!;

    /// <summary>利用者管理の操作対象。</summary>
    public TestUser UserTarget { get; private init; } = null!;

    public TestProject Project { get; private init; } = null!;

    public string ProjectKey => Project.Key;

    public int TaskNumber { get; private init; }

    public Guid CommentId { get; private init; }

    public Guid AttachmentId { get; private init; }

    public Guid TagId { get; private init; }

    public Guid MilestoneId { get; private init; }

    public Guid ExtraStatusId { get; private init; }

    public IReadOnlyList<Guid> StatusIds { get; private init; } = [];

    /// <summary>別の利用者（Candidate）宛てのお知らせ。</summary>
    public Guid OthersNoticeId { get; private init; }

    public static async Task<AuthorizationWorld> CreateAsync(ApiFactory factory, bool archived)
    {
        ArgumentNullException.ThrowIfNull(factory);
        var data = factory.Data;
        var owner = await data.CreateUserAsync(displayName: "認可 管理者");
        var editor = await data.CreateUserAsync(displayName: "認可 編集者");
        var viewer = await data.CreateUserAsync(displayName: "認可 閲覧者");
        var temporary = await data.CreateUserAsync(displayName: "認可 一時パスワード", mustChangePassword: true);
        var memberTarget = await data.CreateUserAsync(displayName: "認可 対象メンバー");
        var project = await data.CreateProjectAsync(owner);
        await data.AddMemberAsync(project, editor, ProjectRole.Editor);
        await data.AddMemberAsync(project, viewer, ProjectRole.Viewer);
        await data.AddMemberAsync(project, temporary, ProjectRole.Owner);
        await data.AddMemberAsync(project, memberTarget, ProjectRole.Viewer);
        var task = await data.CreateTaskAsync(project, owner, "認可確認の対象タスク");
        var comment = await data.CreateCommentAsync(task, owner);
        var attachment = await data.CreateAttachmentAsync(task, owner);
        var tag = await data.CreateTagAsync(project);
        var milestone = await data.CreateMilestoneAsync(project);
        var extra = await data.CreateStatusAsync(project, "保留中");
        var candidate = await data.CreateUserAsync(displayName: "認可 追加候補");
        var notice = await data.QueryAsync(async db =>
        {
            var n = Notice.Create(candidate.Id, NoticeKind.AddedToProject, owner.Id, project.Id, null, "お知らせ", "/projects", DateTime.UtcNow);
            db.Notices.Add(n);
            await db.SaveChangesAsync();
            return n;
        });
        var statuses = await data.GetStatusesAsync(project);
        if (archived)
        {
            await data.ArchiveProjectAsync(project);
        }

        return new AuthorizationWorld
        {
            Owner = owner,
            Editor = editor,
            Viewer = viewer,
            NonMember = await data.CreateUserAsync(displayName: "認可 非メンバー"),
            Administrator = await data.CreateUserAsync(displayName: "認可 システム管理者", administrator: true),
            TemporaryPasswordUser = temporary,
            MemberTarget = memberTarget,
            Candidate = candidate,
            UserTarget = await data.CreateUserAsync(displayName: "認可 利用者管理の対象"),
            Project = project,
            TaskNumber = task.Number,
            CommentId = comment.Id,
            AttachmentId = attachment.Id,
            TagId = tag.Id,
            MilestoneId = milestone.Id,
            ExtraStatusId = extra.Id,
            StatusIds = statuses.Select(s => s.Id).ToList(),
            OthersNoticeId = notice.Id,
        };
    }

    public TestUser? UserFor(Caller caller) => caller switch
    {
        Caller.Anonymous => null,
        Caller.PasswordChangeRequired => TemporaryPasswordUser,
        Caller.NonMember => NonMember,
        Caller.Viewer => Viewer,
        Caller.Editor => Editor,
        Caller.Owner or Caller.ArchivedProjectOwner => Owner,
        Caller.SystemAdministrator => Administrator,
        _ => throw new ArgumentOutOfRangeException(nameof(caller)),
    };
}
