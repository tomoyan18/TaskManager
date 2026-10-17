using TaskManager.Domain.Common;

namespace TaskManager.Domain.Projects;

/// <summary>プロジェクトごとのステータス定義（domain-model.md §4.5）。</summary>
public sealed class WorkflowStatus : IAuditable
{
    public const int NameMaxLength = 30;
    public const int MaxPerProject = 20;

    private WorkflowStatus()
    {
    }

    public Guid Id { get; private set; }

    public Guid ProjectId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public StatusCategory Category { get; private set; }

    public StatusColor Color { get; private set; }

    public ushort SortOrder { get; private set; }

    public bool IsDefault { get; private set; }

    public DateTime CreatedAtUtc { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public static WorkflowStatus Create(
        Guid projectId, string name, StatusCategory category, StatusColor color, int sortOrder, bool isDefault)
    {
        var status = new WorkflowStatus
        {
            Id = Guid.CreateVersion7(),
            ProjectId = projectId,
            Name = NormalizeName(name),
            Category = category,
            Color = color,
            SortOrder = checked((ushort)sortOrder),
        };
        if (isDefault)
        {
            status.MarkAsDefault();
        }

        return status;
    }

    public static string NormalizeName(string? name) =>
        TextRules.RequireSingleLine(name, NameMaxLength, "validation.failed", "name", "ステータス名");

    /// <summary>S-02: 既定にできるのは「未着手」「進行中」の区分のみ。</summary>
    public static bool CanBeDefault(StatusCategory category) =>
        category is StatusCategory.NotStarted or StatusCategory.InProgress;

    public void Rename(string name) => Name = NormalizeName(name);

    public void ChangeColor(StatusColor color) => Color = color;

    public void ChangeCategory(StatusCategory category)
    {
        if (IsDefault && !CanBeDefault(category))
        {
            throw new DomainException(
                "validation.failed", "既定のステータスの区分は「未着手」か「進行中」にしてください。", "category");
        }

        Category = category;
    }

    public void MarkAsDefault()
    {
        if (!CanBeDefault(Category))
        {
            throw new DomainException(
                "validation.failed", "既定にできるのは区分が「未着手」か「進行中」のステータスだけです。", "isDefault");
        }

        IsDefault = true;
    }

    public void UnmarkDefault() => IsDefault = false;

    public void MoveTo(int sortOrder) => SortOrder = checked((ushort)sortOrder);
}
