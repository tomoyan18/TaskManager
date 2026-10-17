namespace TaskManager.Api.Security;

/// <summary>一時パスワード状態でも呼べる操作（パスワード変更・自分の情報・ログアウト・トークン更新）。</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowWhenPasswordChangeRequiredAttribute : Attribute;

/// <summary>OpenAPI の x-required-permission に出す必要な権限（api-design.md §2.2）。</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequiredPermissionAttribute(string permission, string description) : Attribute
{
    public string Permission { get; } = permission;

    /// <summary>日本語の説明（例: プロジェクトの編集者以上）。</summary>
    public string Description { get; } = description;
}

/// <summary>Cookie を使う操作（refresh / logout）で、送信元が自オリジンであることを確認する（CSRF 対策）。</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequireSameOriginAttribute : Attribute;

public static class Policies
{
    public const string SystemAdministrator = "SystemAdministrator";
}
