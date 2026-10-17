using System.Net.Http.Json;
using System.Text;

namespace TaskManager.Api.Tests.Authorization;

/// <summary>呼び出し元の種類（testing-strategy.md §5）。</summary>
public enum Caller
{
    Anonymous,
    PasswordChangeRequired,
    NonMember,
    Viewer,
    Editor,
    Owner,
    SystemAdministrator,
    ArchivedProjectOwner,
}

/// <summary>エンドポイントのアクセス区分。期待するステータスは <see cref="AuthorizationMatrix.Expected"/> で決める。</summary>
public enum AccessRule
{
    /// <summary>誰でも呼べる。一時パスワード状態のトークンを付けても同じ（authentication-authorization.md §3.3）。</summary>
    Public,

    /// <summary>ログイン必須。一時パスワード状態でも呼べる。</summary>
    SignedInAllowTemporary,

    /// <summary>ログイン必須。一時パスワード状態は 403。</summary>
    SignedIn,

    /// <summary>システム管理者のみ。</summary>
    SystemAdministrator,

    /// <summary>いずれかのプロジェクトの管理者、またはシステム管理者。</summary>
    AnyProjectOwnerOrAdministrator,

    /// <summary>プロジェクトのメンバー（閲覧者以上）。アーカイブ中も可。</summary>
    ProjectView,

    /// <summary>編集者以上。アーカイブ中は 409。</summary>
    ProjectEdit,

    /// <summary>管理者のみ。アーカイブ中は 409。</summary>
    ProjectOwner,

    /// <summary>管理者のみ。システム管理者は非メンバーでも可（管理者不在時の救済）。</summary>
    ProjectMembers,

    /// <summary>管理者のみ。アーカイブ中も可（アーカイブの解除）。</summary>
    ProjectArchive,

    /// <summary>システム管理者のみ（アーカイブ後）。</summary>
    ProjectDelete,
}

/// <summary>認可マトリクスの 1 行。Build はテストごとに作るデータ（<see cref="AuthorizationWorld"/>）から要求を作る。</summary>
public sealed record EndpointCase(
    string OperationId, AccessRule Rule, int AllowedStatus, Func<AuthorizationWorld, HttpRequestMessage> Build)
{
    public override string ToString() => OperationId;
}

/// <summary>
/// 全エンドポイントの認可の期待値（docs/design/testing-strategy.md §5）。新しいエンドポイントを追加したら、ここに 1 行追加する
/// （OpenAPI 文書の全 operationId がここにあることを AuthorizationMatrixTests で検査する）。
/// </summary>
public static class AuthorizationMatrix
{
    public static readonly IReadOnlyList<EndpointCase> Endpoints =
    [
        // 認証
        new("Login", AccessRule.Public, 401, w => Json(HttpMethod.Post, "/api/v1/auth/login", new { email = w.Owner.Email, password = "wrong-password-123" })),
        new("RefreshAccessToken", AccessRule.Public, 401, _ => SameOrigin(HttpMethod.Post, "/api/v1/auth/refresh")),
        new("Logout", AccessRule.Public, 204, _ => SameOrigin(HttpMethod.Post, "/api/v1/auth/logout")),
        new("ChangePassword", AccessRule.SignedInAllowTemporary, 400,
            _ => Json(HttpMethod.Post, "/api/v1/auth/password", new { currentPassword = "wrong-password-123", newPassword = "Kanagawa-Minato-2026" })),
        new("GetSystemInfo", AccessRule.Public, 200, _ => new(HttpMethod.Get, "/api/v1/system/info")),

        // 自分
        new("GetMe", AccessRule.SignedInAllowTemporary, 200, _ => new(HttpMethod.Get, "/api/v1/me")),
        new("UpdateMe", AccessRule.SignedIn, 200, _ => Json(HttpMethod.Patch, "/api/v1/me", new { timeZoneId = "Asia/Tokyo" })),
        new("GetMyTasks", AccessRule.SignedIn, 200, _ => new(HttpMethod.Get, "/api/v1/me/tasks?includeRecentlyCompleted=true")),
        new("ListMyNotices", AccessRule.SignedIn, 200, _ => new(HttpMethod.Get, "/api/v1/me/notices?unreadOnly=true")),
        new("GetMyUnreadNoticeCount", AccessRule.SignedIn, 200, _ => new(HttpMethod.Get, "/api/v1/me/notices/unread-count")),
        new("MarkNoticeRead", AccessRule.SignedIn, 404, w => new(HttpMethod.Post, $"/api/v1/me/notices/{w.OthersNoticeId}/read")),
        new("MarkAllNoticesRead", AccessRule.SignedIn, 204, _ => new(HttpMethod.Post, "/api/v1/me/notices/read-all")),
        new("SearchTasks", AccessRule.SignedIn, 200, w => new(HttpMethod.Get, $"/api/v1/search/tasks?q={w.ProjectKey}")),
        new("GetMyTaskStatusSummary", AccessRule.SignedIn, 200, _ => new(HttpMethod.Get, "/api/v1/me/tasks/status-summary")),

        // プロジェクト
        new("ListProjects", AccessRule.SignedIn, 200, _ => new(HttpMethod.Get, "/api/v1/projects?includeArchived=true")),
        new("CreateProject", AccessRule.SignedIn, 201, _ => Json(HttpMethod.Post, "/api/v1/projects",
            new { key = TaskManager.Testing.Unique.ProjectKey(), name = TaskManager.Testing.Unique.Name("認可確認") })),
        new("GetProject", AccessRule.ProjectView, 200, w => new(HttpMethod.Get, $"/api/v1/projects/{w.ProjectKey}")),
        new("UpdateProject", AccessRule.ProjectOwner, 200, w => Json(HttpMethod.Patch, $"/api/v1/projects/{w.ProjectKey}", new { description = "変更", version = 0 })),
        new("DeleteProject", AccessRule.ProjectDelete, 204, w => new(HttpMethod.Delete, $"/api/v1/projects/{w.ProjectKey}?confirmKey={w.ProjectKey}")),
        new("ArchiveProject", AccessRule.ProjectArchive, 200, w => new(HttpMethod.Post, $"/api/v1/projects/{w.ProjectKey}/archive")),
        new("UnarchiveProject", AccessRule.ProjectArchive, 200, w => new(HttpMethod.Post, $"/api/v1/projects/{w.ProjectKey}/unarchive")),

        // メンバー
        new("ListProjectMembers", AccessRule.ProjectView, 200, w => new(HttpMethod.Get, $"/api/v1/projects/{w.ProjectKey}/members")),
        new("AddProjectMember", AccessRule.ProjectMembers, 201, w => Json(HttpMethod.Post, $"/api/v1/projects/{w.ProjectKey}/members",
            new { userId = w.Candidate.Id, role = "viewer" })),
        new("ChangeProjectMemberRole", AccessRule.ProjectMembers, 200, w => Json(HttpMethod.Patch,
            $"/api/v1/projects/{w.ProjectKey}/members/{w.MemberTarget.Id}", new { role = "editor" })),
        new("RemoveProjectMember", AccessRule.ProjectMembers, 204, w => new(HttpMethod.Delete, $"/api/v1/projects/{w.ProjectKey}/members/{w.MemberTarget.Id}")),
        new("SearchUserDirectory", AccessRule.AnyProjectOwnerOrAdministrator, 200, w => new(HttpMethod.Get, $"/api/v1/user-directory?q=example&excludeProjectKey={w.ProjectKey}")),

        // 利用者管理
        new("ListUsers", AccessRule.SystemAdministrator, 200, _ => new(HttpMethod.Get, "/api/v1/users?status=active")),
        new("CreateUser", AccessRule.SystemAdministrator, 201, _ => Json(HttpMethod.Post, "/api/v1/users",
            new { email = TaskManager.Testing.Unique.Email("matrix"), displayName = "認可確認", systemRole = "member" })),
        new("GetUser", AccessRule.SystemAdministrator, 200, w => new(HttpMethod.Get, $"/api/v1/users/{w.UserTarget.Id}")),
        new("UpdateUser", AccessRule.SystemAdministrator, 200, w => Json(HttpMethod.Patch, $"/api/v1/users/{w.UserTarget.Id}", new { displayName = "変更後", version = 0 })),
        new("DeactivateUser", AccessRule.SystemAdministrator, 204, w => new(HttpMethod.Post, $"/api/v1/users/{w.UserTarget.Id}/deactivate")),
        new("ActivateUser", AccessRule.SystemAdministrator, 204, w => new(HttpMethod.Post, $"/api/v1/users/{w.UserTarget.Id}/activate")),
        new("UnlockUser", AccessRule.SystemAdministrator, 204, w => new(HttpMethod.Post, $"/api/v1/users/{w.UserTarget.Id}/unlock")),
        new("ResetUserPassword", AccessRule.SystemAdministrator, 200, w => new(HttpMethod.Post, $"/api/v1/users/{w.UserTarget.Id}/password-reset")),

        // タスク
        new("ListTasks", AccessRule.ProjectView, 200, w => new(HttpMethod.Get, $"/api/v1/projects/{w.ProjectKey}/tasks?includeClosed=true&sort=number")),
        new("CreateTask", AccessRule.ProjectEdit, 201, w => Json(HttpMethod.Post, $"/api/v1/projects/{w.ProjectKey}/tasks", new { title = "認可確認のタスク" })),
        new("GetTask", AccessRule.ProjectView, 200, w => new(HttpMethod.Get, $"/api/v1/projects/{w.ProjectKey}/tasks/{w.TaskNumber}")),
        new("UpdateTask", AccessRule.ProjectEdit, 200, w => Json(HttpMethod.Patch, $"/api/v1/projects/{w.ProjectKey}/tasks/{w.TaskNumber}",
            new { title = "変更後のタイトル", version = 0 })),
        // 管理者が作ったタスク: 編集者（作成者以外）は削除できない
        new("DeleteTask", AccessRule.ProjectOwner, 204, w => new(HttpMethod.Delete, $"/api/v1/projects/{w.ProjectKey}/tasks/{w.TaskNumber}")),
        // ガントチャート・ステータス別円グラフ（Phase 9）
        new("GetTaskSchedule", AccessRule.ProjectView, 200, w => new(HttpMethod.Get,
            $"/api/v1/projects/{w.ProjectKey}/tasks/schedule?from=2026-10-01&to=2026-12-31&includeClosed=true")),
        new("GetTaskStatusSummary", AccessRule.ProjectView, 200, w => new(HttpMethod.Get, $"/api/v1/projects/{w.ProjectKey}/tasks/status-summary")),
        new("ListTaskHistory", AccessRule.ProjectView, 200, w => new(HttpMethod.Get, $"/api/v1/projects/{w.ProjectKey}/tasks/{w.TaskNumber}/history")),
        new("ListTaskComments", AccessRule.ProjectView, 200, w => new(HttpMethod.Get, $"/api/v1/projects/{w.ProjectKey}/tasks/{w.TaskNumber}/comments")),
        new("CreateTaskComment", AccessRule.ProjectEdit, 201, w => Json(HttpMethod.Post, $"/api/v1/projects/{w.ProjectKey}/tasks/{w.TaskNumber}/comments",
            new { body = "認可確認のコメント" })),
        // 管理者が書いたコメント: 本人以外は編集できない（管理者でも 403。投稿者本人の編集は別のテストで確認）
        new("UpdateTaskComment", AccessRule.ProjectOwner, 200, w => Json(HttpMethod.Patch,
            $"/api/v1/projects/{w.ProjectKey}/tasks/{w.TaskNumber}/comments/{w.CommentId}", new { body = "編集後" })),
        new("DeleteTaskComment", AccessRule.ProjectOwner, 204, w => new(HttpMethod.Delete,
            $"/api/v1/projects/{w.ProjectKey}/tasks/{w.TaskNumber}/comments/{w.CommentId}")),
        new("ListTaskAttachments", AccessRule.ProjectView, 200, w => new(HttpMethod.Get, $"/api/v1/projects/{w.ProjectKey}/tasks/{w.TaskNumber}/attachments")),
        new("UploadTaskAttachment", AccessRule.ProjectEdit, 201, w => Upload($"/api/v1/projects/{w.ProjectKey}/tasks/{w.TaskNumber}/attachments", "memo.txt", "認可確認")),
        new("DownloadTaskAttachment", AccessRule.ProjectView, 200, w => new(HttpMethod.Get,
            $"/api/v1/projects/{w.ProjectKey}/tasks/{w.TaskNumber}/attachments/{w.AttachmentId}/content")),
        new("DeleteTaskAttachment", AccessRule.ProjectOwner, 204, w => new(HttpMethod.Delete,
            $"/api/v1/projects/{w.ProjectKey}/tasks/{w.TaskNumber}/attachments/{w.AttachmentId}")),

        // ステータス・タグ・マイルストーン
        new("ListWorkflowStatuses", AccessRule.ProjectView, 200, w => new(HttpMethod.Get, $"/api/v1/projects/{w.ProjectKey}/statuses")),
        new("CreateWorkflowStatus", AccessRule.ProjectOwner, 201, w => Json(HttpMethod.Post, $"/api/v1/projects/{w.ProjectKey}/statuses",
            new { name = "追加の状態", category = "inProgress", color = "teal" })),
        new("UpdateWorkflowStatus", AccessRule.ProjectOwner, 200, w => Json(HttpMethod.Patch, $"/api/v1/projects/{w.ProjectKey}/statuses/{w.ExtraStatusId}",
            new { name = "名前を変更" })),
        new("DeleteWorkflowStatus", AccessRule.ProjectOwner, 204, w => new(HttpMethod.Delete, $"/api/v1/projects/{w.ProjectKey}/statuses/{w.ExtraStatusId}")),
        new("ReorderWorkflowStatuses", AccessRule.ProjectOwner, 200, w => Json(HttpMethod.Put, $"/api/v1/projects/{w.ProjectKey}/statuses/order",
            new { statusIds = w.StatusIds.Reverse().ToArray() })),
        new("ListTags", AccessRule.ProjectView, 200, w => new(HttpMethod.Get, $"/api/v1/projects/{w.ProjectKey}/tags")),
        new("CreateTag", AccessRule.ProjectEdit, 201, w => Json(HttpMethod.Post, $"/api/v1/projects/{w.ProjectKey}/tags", new { name = "認可確認", color = "green" })),
        new("UpdateTag", AccessRule.ProjectEdit, 200, w => Json(HttpMethod.Patch, $"/api/v1/projects/{w.ProjectKey}/tags/{w.TagId}", new { name = "変更後のタグ" })),
        new("DeleteTag", AccessRule.ProjectEdit, 204, w => new(HttpMethod.Delete, $"/api/v1/projects/{w.ProjectKey}/tags/{w.TagId}")),
        new("ListMilestones", AccessRule.ProjectView, 200, w => new(HttpMethod.Get, $"/api/v1/projects/{w.ProjectKey}/milestones")),
        new("CreateMilestone", AccessRule.ProjectEdit, 201, w => Json(HttpMethod.Post, $"/api/v1/projects/{w.ProjectKey}/milestones",
            new { name = "認可確認", targetDate = "2026-12-31" })),
        new("GetMilestone", AccessRule.ProjectView, 200, w => new(HttpMethod.Get, $"/api/v1/projects/{w.ProjectKey}/milestones/{w.MilestoneId}")),
        new("UpdateMilestone", AccessRule.ProjectEdit, 200, w => Json(HttpMethod.Patch, $"/api/v1/projects/{w.ProjectKey}/milestones/{w.MilestoneId}",
            new { name = "変更後", version = 0 })),
        new("DeleteMilestone", AccessRule.ProjectEdit, 204, w => new(HttpMethod.Delete, $"/api/v1/projects/{w.ProjectKey}/milestones/{w.MilestoneId}")),
    ];

    /// <summary>区分と呼び出し元から、期待するステータスを決める（allowed = 許可された場合のステータス）。</summary>
    public static int Expected(AccessRule rule, Caller caller, int allowed)
    {
        if (rule == AccessRule.Public)
        {
            return allowed;
        }

        if (caller == Caller.Anonymous)
        {
            return 401;
        }

        if (caller == Caller.PasswordChangeRequired)
        {
            return rule == AccessRule.SignedInAllowTemporary ? allowed : 403;
        }

        return rule switch
        {
            AccessRule.SignedInAllowTemporary or AccessRule.SignedIn => allowed,
            AccessRule.SystemAdministrator => caller == Caller.SystemAdministrator ? allowed : 403,
            AccessRule.AnyProjectOwnerOrAdministrator =>
                caller is Caller.Owner or Caller.ArchivedProjectOwner or Caller.SystemAdministrator ? allowed : 403,
            AccessRule.ProjectDelete => caller == Caller.SystemAdministrator ? 409 /* アーカイブ前は削除できない */ : 403,
            _ => Project(rule, caller, allowed),
        };
    }

    private static int Project(AccessRule rule, Caller caller, int allowed) => caller switch
    {
        Caller.NonMember => 404,
        Caller.SystemAdministrator => rule == AccessRule.ProjectMembers ? allowed : 404,
        Caller.Viewer => rule == AccessRule.ProjectView ? allowed : 403,
        Caller.Editor => rule is AccessRule.ProjectView or AccessRule.ProjectEdit ? allowed : 403,
        Caller.Owner => allowed,
        Caller.ArchivedProjectOwner => rule is AccessRule.ProjectView or AccessRule.ProjectArchive ? allowed : 409,
        _ => throw new ArgumentOutOfRangeException(nameof(caller)),
    };

    public static TheoryData<string, Caller> Cases()
    {
        var data = new TheoryData<string, Caller>();
        foreach (var endpoint in Endpoints)
        {
            foreach (var caller in Enum.GetValues<Caller>())
            {
                data.Add(endpoint.OperationId, caller);
            }
        }

        return data;
    }

    public static EndpointCase Find(string operationId) => Endpoints.Single(e => e.OperationId == operationId);

    private static HttpRequestMessage Json(HttpMethod method, string url, object body) =>
        new(method, url) { Content = JsonContent.Create(body) };

    private static HttpRequestMessage SameOrigin(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("Origin", "https://localhost");
        return request;
    }

    internal static HttpRequestMessage Upload(string url, string fileName, string text)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(text));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", fileName);
        return new HttpRequestMessage(HttpMethod.Post, url) { Content = form };
    }
}
