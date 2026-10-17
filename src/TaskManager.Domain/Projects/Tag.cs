using TaskManager.Domain.Common;

namespace TaskManager.Domain.Projects;

/// <summary>タグ（domain-model.md §4.6）。</summary>
public sealed class Tag : IAuditable
{
    public const int NameMaxLength = 30;
    public const int MaxPerProject = 50;

    private Tag()
    {
    }

    public Guid Id { get; private set; }

    public Guid ProjectId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public StatusColor Color { get; private set; }

    public DateTime CreatedAtUtc { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public static Tag Create(Guid projectId, string name, StatusColor color) => new()
    {
        Id = Guid.CreateVersion7(),
        ProjectId = projectId,
        Name = NormalizeName(name),
        Color = color,
    };

    public static string NormalizeName(string? name) =>
        TextRules.RequireSingleLine(name, NameMaxLength, "validation.failed", "name", "タグ名");

    public void Rename(string name) => Name = NormalizeName(name);

    public void ChangeColor(StatusColor color) => Color = color;
}
