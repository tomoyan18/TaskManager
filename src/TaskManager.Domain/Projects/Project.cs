using TaskManager.Domain.Common;

namespace TaskManager.Domain.Projects;

/// <summary>プロジェクト（domain-model.md §4.3）。キーは作成後に変更できない（P-01）。</summary>
public sealed class Project : IAuditable, IVersioned
{
    public const int NameMaxLength = 80;
    public const int DescriptionMaxLength = 2000;

    private Project()
    {
    }

    public Guid Id { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public DateTime? ArchivedAtUtc { get; private set; }

    public uint LastTaskNumber { get; private set; }

    public uint RowVersion { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public bool IsArchived => ArchivedAtUtc is not null;

    public static Project Create(ProjectKey key, string name, string? description)
    {
        return new Project
        {
            Id = Guid.CreateVersion7(),
            Key = key.Value,
            Name = NormalizeName(name),
            Description = NormalizeDescription(description),
        };
    }

    public static string NormalizeName(string? name) =>
        TextRules.RequireSingleLine(name, NameMaxLength, "validation.failed", "name", "プロジェクト名");

    public static string? NormalizeDescription(string? description) =>
        TextRules.OptionalMultiline(description, DescriptionMaxLength, "validation.failed", "description", "説明");

    public void Rename(string name) => Name = NormalizeName(name);

    public void ChangeDescription(string? description) => Description = NormalizeDescription(description);

    public void Archive(DateTime nowUtc) => ArchivedAtUtc ??= nowUtc;

    public void Unarchive() => ArchivedAtUtc = null;

    /// <summary>P-04: 作成時の既定ステータス 5 件。</summary>
    public IReadOnlyList<WorkflowStatus> CreateDefaultStatuses() =>
    [
        WorkflowStatus.Create(Id, "未着手", StatusCategory.NotStarted, StatusColor.Gray, 1, isDefault: true),
        WorkflowStatus.Create(Id, "対応中", StatusCategory.InProgress, StatusColor.Blue, 2, isDefault: false),
        WorkflowStatus.Create(Id, "確認待ち", StatusCategory.InProgress, StatusColor.Purple, 3, isDefault: false),
        WorkflowStatus.Create(Id, "完了", StatusCategory.Done, StatusColor.Green, 4, isDefault: false),
        WorkflowStatus.Create(Id, "中止", StatusCategory.Canceled, StatusColor.Red, 5, isDefault: false),
    ];
}
