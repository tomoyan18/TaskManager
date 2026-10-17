using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using TaskManager.Application.Common;
using TaskManager.Domain.Common;

namespace TaskManager.Application.Projects;

public sealed record ProjectSummaryResponse(
    string Key,
    string Name,
    ProjectRole? MyRole,
    bool IsArchived,
    int OpenTaskCount,
    int OverdueTaskCount,
    IReadOnlyList<string> OwnerNames,
    DateTime UpdatedAtUtc);

public sealed record ProjectTaskCounts(int NotStarted, int InProgress, int Done, int Canceled, int Overdue);

public sealed record ProjectDetailResponse(
    string Key,
    string Name,
    string? Description,
    bool IsArchived,
    DateTime? ArchivedAtUtc,
    ProjectRole? MyRole,
    int MemberCount,
    ProjectTaskCounts TaskCounts,
    DateTime CreatedAtUtc,
    UserRef? CreatedBy,
    DateTime UpdatedAtUtc,
    uint Version);

public sealed class CreateProjectRequest
{
    [Display(Name = "プロジェクトキー")]
    [Required(ErrorMessage = ValidationMessages.Required)]
    [StringLength(10, MinimumLength = 2, ErrorMessage = ValidationMessages.LengthRange)]
    [RegularExpression(ProjectKey.Pattern, ErrorMessage = ValidationMessages.ProjectKey)]
    public string Key { get; set; } = string.Empty;

    [Display(Name = "プロジェクト名")]
    [Required(ErrorMessage = ValidationMessages.Required)]
    [StringLength(80, MinimumLength = 1, ErrorMessage = ValidationMessages.LengthRange)]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "説明")]
    [StringLength(2000, ErrorMessage = ValidationMessages.MaxLength)]
    public string? Description { get; set; }
}

public sealed class UpdateProjectRequest : PatchRequest
{
    private string? _name;
    private string? _description;

    [Display(Name = "プロジェクト名")]
    [StringLength(80, MinimumLength = 1, ErrorMessage = ValidationMessages.LengthRange)]
    public string? Name { get => _name; set => _name = Set(value); }

    [Display(Name = "説明")]
    [StringLength(2000, ErrorMessage = ValidationMessages.MaxLength)]
    public string? Description { get => _description; set => _description = Set(value); }

    /// <summary>取得時の version（楽観的排他）。</summary>
    [Display(Name = "版")]
    [JsonRequired]
    public uint Version { get; set; }
}

public sealed record ProjectMemberResponse(UserRef User, string Email, ProjectRole Role, DateTime JoinedAtUtc);

public sealed class AddProjectMemberRequest
{
    [Display(Name = "利用者")]
    [Required(ErrorMessage = ValidationMessages.Select)]
    public Guid? UserId { get; set; }

    [Display(Name = "ロール")]
    [Required(ErrorMessage = ValidationMessages.Select)]
    public ProjectRole? Role { get; set; }
}

public sealed class ChangeProjectMemberRoleRequest
{
    [Display(Name = "ロール")]
    [Required(ErrorMessage = ValidationMessages.Select)]
    public ProjectRole? Role { get; set; }
}
