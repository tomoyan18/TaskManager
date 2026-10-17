using TaskManager.Domain.Common;

namespace TaskManager.Domain.Projects;

/// <summary>利用者のプロジェクトへの所属とロール（domain-model.md §4.4）。</summary>
public sealed class ProjectMembership
{
    private ProjectMembership()
    {
    }

    public Guid Id { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid UserId { get; private set; }

    public ProjectRole Role { get; private set; }

    public DateTime JoinedAtUtc { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public Guid UpdatedByUserId { get; private set; }

    public static ProjectMembership Create(Guid projectId, Guid userId, ProjectRole role, Guid actorUserId, DateTime nowUtc)
    {
        return new ProjectMembership
        {
            Id = Guid.CreateVersion7(),
            ProjectId = projectId,
            UserId = userId,
            Role = role,
            JoinedAtUtc = nowUtc,
            CreatedByUserId = actorUserId,
            UpdatedAtUtc = nowUtc,
            UpdatedByUserId = actorUserId,
        };
    }

    public void ChangeRole(ProjectRole role, Guid actorUserId, DateTime nowUtc)
    {
        Role = role;
        UpdatedByUserId = actorUserId;
        UpdatedAtUtc = nowUtc;
    }
}
