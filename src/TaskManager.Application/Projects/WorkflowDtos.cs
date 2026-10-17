using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using TaskManager.Application.Common;
using TaskManager.Domain.Common;

namespace TaskManager.Application.Projects;

public sealed record WorkflowStatusResponse(
    Guid Id, string Name, StatusCategory Category, StatusColor Color, int SortOrder, bool IsDefault, int TaskCount);

public sealed class CreateWorkflowStatusRequest
{
    [Display(Name = "ステータス名")]
    [Required(ErrorMessage = ValidationMessages.Required)]
    [StringLength(30, MinimumLength = 1, ErrorMessage = ValidationMessages.LengthRange)]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "区分")]
    [Required(ErrorMessage = ValidationMessages.Select)]
    public StatusCategory? Category { get; set; }

    [Display(Name = "色")]
    [Required(ErrorMessage = ValidationMessages.Select)]
    public StatusColor? Color { get; set; }
}

public sealed class UpdateWorkflowStatusRequest : PatchRequest
{
    private string? _name;
    private StatusCategory? _category;
    private StatusColor? _color;
    private bool? _isDefault;

    [Display(Name = "ステータス名")]
    [StringLength(30, MinimumLength = 1, ErrorMessage = ValidationMessages.LengthRange)]
    public string? Name { get => _name; set => _name = Set(value); }

    [Display(Name = "区分")]
    public StatusCategory? Category { get => _category; set => _category = Set(value); }

    [Display(Name = "色")]
    public StatusColor? Color { get => _color; set => _color = Set(value); }

    /// <summary>true のみ受け付ける（他のステータスの既定は自動で外れる）。</summary>
    [Display(Name = "既定")]
    public bool? IsDefault { get => _isDefault; set => _isDefault = Set(value); }
}

public sealed class ReorderWorkflowStatusesRequest
{
    [Display(Name = "ステータスの並び")]
    [Required(ErrorMessage = ValidationMessages.Required)]
    [MaxLength(20, ErrorMessage = ValidationMessages.MaxItems)]
    public IReadOnlyList<Guid> StatusIds { get; set; } = [];
}

public sealed record TagResponse(Guid Id, string Name, StatusColor Color, int TaskCount);

public sealed class CreateTagRequest
{
    [Display(Name = "タグ名")]
    [Required(ErrorMessage = ValidationMessages.Required)]
    [StringLength(30, MinimumLength = 1, ErrorMessage = ValidationMessages.LengthRange)]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "色")]
    [Required(ErrorMessage = ValidationMessages.Select)]
    public StatusColor? Color { get; set; }
}

public sealed class UpdateTagRequest : PatchRequest
{
    private string? _name;
    private StatusColor? _color;

    [Display(Name = "タグ名")]
    [StringLength(30, MinimumLength = 1, ErrorMessage = ValidationMessages.LengthRange)]
    public string? Name { get => _name; set => _name = Set(value); }

    [Display(Name = "色")]
    public StatusColor? Color { get => _color; set => _color = Set(value); }
}

public sealed record MilestoneSummaryResponse(
    Guid Id, string Name, DateOnly? TargetDate, MilestoneState State, int TotalTaskCount, int ClosedTaskCount, int OverdueTaskCount);

public sealed record MilestoneDetailResponse(
    Guid Id, string Name, string? Description, DateOnly? TargetDate, MilestoneState State,
    int TotalTaskCount, int ClosedTaskCount, int OverdueTaskCount, uint Version, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);

public sealed class CreateMilestoneRequest
{
    [Display(Name = "マイルストーン名")]
    [Required(ErrorMessage = ValidationMessages.Required)]
    [StringLength(80, MinimumLength = 1, ErrorMessage = ValidationMessages.LengthRange)]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "説明")]
    [StringLength(2000, ErrorMessage = ValidationMessages.MaxLength)]
    public string? Description { get; set; }

    [Display(Name = "目標日")]
    public DateOnly? TargetDate { get; set; }
}

public sealed class UpdateMilestoneRequest : PatchRequest
{
    private string? _name;
    private string? _description;
    private DateOnly? _targetDate;
    private MilestoneState? _state;

    [Display(Name = "マイルストーン名")]
    [StringLength(80, MinimumLength = 1, ErrorMessage = ValidationMessages.LengthRange)]
    public string? Name { get => _name; set => _name = Set(value); }

    [Display(Name = "説明")]
    [StringLength(2000, ErrorMessage = ValidationMessages.MaxLength)]
    public string? Description { get => _description; set => _description = Set(value); }

    [Display(Name = "目標日")]
    public DateOnly? TargetDate { get => _targetDate; set => _targetDate = Set(value); }

    [Display(Name = "状態")]
    public MilestoneState? State { get => _state; set => _state = Set(value); }

    /// <summary>取得時の version（楽観的排他）。</summary>
    [Display(Name = "版")]
    [JsonRequired]
    public uint Version { get; set; }
}
