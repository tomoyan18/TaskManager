using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Common;
using TaskManager.Application.Security;
using TaskManager.Domain.Common;
using TaskManager.Domain.Projects;
using TaskManager.Domain.Tasks;

namespace TaskManager.Application.Projects;

/// <summary>マイルストーン（REQ-MLS-01）。</summary>
public sealed class MilestoneService(
    IAppDbContext db, ICurrentUser currentUser, ProjectAccessGuard guard, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<MilestoneSummaryResponse>> ListAsync(
        string projectKey, MilestoneState? state, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.ViewProject, cancellationToken);
        var query = db.Milestones.AsNoTracking().Where(m => m.ProjectId == access.ProjectId);
        if (state is { } s)
        {
            query = query.Where(m => m.State == s);
        }

        var milestones = await query.ToListAsync(cancellationToken);
        var stats = await StatsAsync(access.ProjectId, cancellationToken);
        return milestones
            .OrderBy(m => m.State)
            .ThenBy(m => m.TargetDate is null)
            .ThenBy(m => m.TargetDate)
            .ThenBy(m => m.Name, StringComparer.Ordinal)
            .Select(m =>
            {
                var st = stats.GetValueOrDefault(m.Id);
                return new MilestoneSummaryResponse(m.Id, m.Name, m.TargetDate, m.State, st.Total, st.Closed, st.Overdue);
            })
            .ToList();
    }

    public async Task<MilestoneDetailResponse> GetAsync(string projectKey, Guid milestoneId, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.ViewProject, cancellationToken);
        var milestone = await db.Milestones.AsNoTracking()
            .SingleOrDefaultAsync(m => m.ProjectId == access.ProjectId && m.Id == milestoneId, cancellationToken)
            ?? throw Errors.NotFound();
        return await ToDetailAsync(milestone, cancellationToken);
    }

    public async Task<MilestoneDetailResponse> CreateAsync(
        string projectKey, CreateMilestoneRequest request, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.ManageMilestones, cancellationToken);
        Milestone milestone;
        try
        {
            milestone = Milestone.Create(access.ProjectId, request.Name, request.Description, request.TargetDate);
        }
        catch (DomainException ex)
        {
            throw Errors.FromDomain(ex);
        }

        await EnsureUniqueNameAsync(access.ProjectId, milestone.Name, null, cancellationToken);
        db.Milestones.Add(milestone);
        await db.SaveChangesAsync(cancellationToken);
        return await ToDetailAsync(milestone, cancellationToken);
    }

    public async Task<MilestoneDetailResponse> UpdateAsync(
        string projectKey, Guid milestoneId, UpdateMilestoneRequest request, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.ManageMilestones, cancellationToken);
        var milestone = await db.Milestones
            .SingleOrDefaultAsync(m => m.ProjectId == access.ProjectId && m.Id == milestoneId, cancellationToken)
            ?? throw Errors.NotFound();
        db.SetExpectedVersion(milestone, request.Version);
        if (milestone.RowVersion != request.Version)
        {
            throw Errors.ConcurrencyConflict();
        }

        try
        {
            if (request.IsSpecified(nameof(request.Name)))
            {
                var name = Milestone.NormalizeName(request.Name);
                await EnsureUniqueNameAsync(access.ProjectId, name, milestone.Id, cancellationToken);
                milestone.Rename(name);
            }

            if (request.IsSpecified(nameof(request.Description)))
            {
                milestone.ChangeDescription(request.Description);
            }

            if (request.IsSpecified(nameof(request.TargetDate)))
            {
                milestone.ChangeTargetDate(request.TargetDate);
            }

            if (request.IsSpecified(nameof(request.State)) && request.State is { } state)
            {
                milestone.ChangeState(state);
            }
        }
        catch (DomainException ex)
        {
            throw Errors.FromDomain(ex);
        }

        await db.SaveChangesAsync(cancellationToken);
        return await ToDetailAsync(milestone, cancellationToken);
    }

    /// <summary>MS-01: 削除するとタスクとの関連を外し、各タスクに履歴を残す。</summary>
    public async Task DeleteAsync(string projectKey, Guid milestoneId, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.ManageMilestones, cancellationToken);
        var milestone = await db.Milestones
            .SingleOrDefaultAsync(m => m.ProjectId == access.ProjectId && m.Id == milestoneId, cancellationToken)
            ?? throw Errors.NotFound();
        var now = timeProvider.UtcNow();
        var tasks = await db.TaskItems.Where(t => t.MilestoneId == milestone.Id).ToListAsync(cancellationToken);
        foreach (var task in tasks)
        {
            task.ChangeMilestone(null);
            db.TaskHistoryEntries.Add(TaskHistoryEntry.Create(
                task.Id, TaskHistoryKind.MilestoneChanged, currentUser.UserId, milestone.Name, null, now));
        }

        db.Milestones.Remove(milestone);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureUniqueNameAsync(Guid projectId, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        if (await db.Milestones.AnyAsync(m => m.ProjectId == projectId && m.Name == name && m.Id != exceptId, cancellationToken))
        {
            throw Errors.Conflict(ErrorCodes.MilestoneNameTaken, "同じ名前のマイルストーンが既にあります。");
        }
    }

    private readonly record struct MilestoneStats(int Total, int Closed, int Overdue);

    private async Task<Dictionary<Guid, MilestoneStats>> StatsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var tz = await db.UserAccounts.AsNoTracking().Where(u => u.Id == currentUser.UserId).Select(u => u.TimeZoneId)
            .SingleOrDefaultAsync(cancellationToken);
        var today = timeProvider.TodayIn(tz ?? TimeZones.Default);
        var rows = await db.TaskItems.AsNoTracking()
            .Where(t => t.ProjectId == projectId && t.MilestoneId != null)
            .Join(db.WorkflowStatuses, t => t.StatusId, s => s.Id, (t, s) => new { t.MilestoneId, s.Category, t.DueDate })
            .ToListAsync(cancellationToken);
        return rows.GroupBy(r => r.MilestoneId!.Value).ToDictionary(
            g => g.Key,
            g => new MilestoneStats(
                g.Count(),
                g.Count(r => r.Category.IsClosed()),
                g.Count(r => TaskItem.IsOverdue(r.DueDate, r.Category, today))));
    }

    private async Task<MilestoneDetailResponse> ToDetailAsync(Milestone m, CancellationToken cancellationToken)
    {
        var st = (await StatsAsync(m.ProjectId, cancellationToken)).GetValueOrDefault(m.Id);
        return new MilestoneDetailResponse(
            m.Id, m.Name, m.Description, m.TargetDate, m.State, st.Total, st.Closed, st.Overdue, m.RowVersion, m.CreatedAtUtc, m.UpdatedAtUtc);
    }
}
