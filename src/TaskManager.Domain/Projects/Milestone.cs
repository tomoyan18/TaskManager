using TaskManager.Domain.Common;

namespace TaskManager.Domain.Projects;

/// <summary>マイルストーン（domain-model.md §4.7）。</summary>
public sealed class Milestone : IAuditable, IVersioned
{
    public const int NameMaxLength = 80;
    public const int DescriptionMaxLength = 2000;

    private Milestone()
    {
    }

    public Guid Id { get; private set; }

    public Guid ProjectId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public DateOnly? TargetDate { get; private set; }

    public MilestoneState State { get; private set; }

    public uint RowVersion { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public static Milestone Create(Guid projectId, string name, string? description, DateOnly? targetDate) => new()
    {
        Id = Guid.CreateVersion7(),
        ProjectId = projectId,
        Name = NormalizeName(name),
        Description = NormalizeDescription(description),
        TargetDate = targetDate,
        State = MilestoneState.Open,
    };

    public static string NormalizeName(string? name) =>
        TextRules.RequireSingleLine(name, NameMaxLength, "validation.failed", "name", "マイルストーン名");

    public static string? NormalizeDescription(string? description) =>
        TextRules.OptionalMultiline(description, DescriptionMaxLength, "validation.failed", "description", "説明");

    public void Rename(string name) => Name = NormalizeName(name);

    public void ChangeDescription(string? description) => Description = NormalizeDescription(description);

    public void ChangeTargetDate(DateOnly? targetDate) => TargetDate = targetDate;

    public void ChangeState(MilestoneState state) => State = state;
}
