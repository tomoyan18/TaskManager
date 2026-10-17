using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using TaskManager.Application.Common;
using TaskManager.Domain.Common;

namespace TaskManager.Application.Users;

/// <summary>ログイン中の利用者（GET /api/v1/me）。</summary>
public sealed record CurrentUserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    SystemRole SystemRole,
    string TimeZoneId,
    bool MustChangePassword,
    bool IsProjectOwnerSomewhere);

/// <summary>自分のプロフィールの変更（PATCH /api/v1/me）。</summary>
public sealed class UpdateMeRequest : PatchRequest
{
    private string? _displayName;
    private string? _timeZoneId;

    [Display(Name = "表示名")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = ValidationMessages.LengthRange)]
    public string? DisplayName { get => _displayName; set => _displayName = Set(value); }

    [Display(Name = "タイムゾーン")]
    [StringLength(64, MinimumLength = 1, ErrorMessage = ValidationMessages.LengthRange)]
    public string? TimeZoneId { get => _timeZoneId; set => _timeZoneId = Set(value); }
}

/// <summary>利用者管理の一覧の 1 行。</summary>
public sealed record UserAdminSummaryResponse(
    Guid Id,
    string Email,
    string DisplayName,
    SystemRole SystemRole,
    bool IsActive,
    bool IsLocked,
    bool MustChangePassword,
    DateTime? LastLoginAtUtc,
    DateTime CreatedAtUtc);

public sealed record UserAdminDetailResponse(
    Guid Id,
    string Email,
    string DisplayName,
    SystemRole SystemRole,
    bool IsActive,
    bool IsLocked,
    bool MustChangePassword,
    DateTime? LastLoginAtUtc,
    DateTime CreatedAtUtc,
    DateTime? LockedUntilUtc,
    string TimeZoneId,
    int ProjectCount,
    uint Version);

public enum UserStatusFilter
{
    Active,
    Inactive,
    Locked,
}

public sealed class CreateUserRequest
{
    [Display(Name = "メールアドレス")]
    [Required(ErrorMessage = ValidationMessages.Required)]
    [StringLength(254, MinimumLength = 3, ErrorMessage = ValidationMessages.LengthRange)]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "表示名")]
    [Required(ErrorMessage = ValidationMessages.Required)]
    [StringLength(50, MinimumLength = 1, ErrorMessage = ValidationMessages.LengthRange)]
    public string DisplayName { get; set; } = string.Empty;

    [Display(Name = "システムロール")]
    [Required(ErrorMessage = ValidationMessages.Select)]
    public SystemRole? SystemRole { get; set; }
}

/// <summary>作成結果。一時パスワードはこの応答でのみ返す。</summary>
public sealed record CreatedUserResponse(UserAdminSummaryResponse User, string TemporaryPassword);

public sealed record TemporaryPasswordResponse(string TemporaryPassword);

public sealed class UpdateUserRequest : PatchRequest
{
    private string? _displayName;
    private SystemRole? _systemRole;

    [Display(Name = "表示名")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = ValidationMessages.LengthRange)]
    public string? DisplayName { get => _displayName; set => _displayName = Set(value); }

    [Display(Name = "システムロール")]
    public SystemRole? SystemRole { get => _systemRole; set => _systemRole = Set(value); }

    /// <summary>取得時の version（楽観的排他）。</summary>
    [Display(Name = "版")]
    [JsonRequired]
    public uint Version { get; set; }
}

/// <summary>メンバー追加用の利用者検索結果。</summary>
public sealed record UserDirectoryEntry(Guid Id, string DisplayName, string Email);
