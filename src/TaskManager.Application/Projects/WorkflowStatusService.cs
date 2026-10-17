using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Common;
using TaskManager.Application.Security;
using TaskManager.Domain.Common;
using TaskManager.Domain.Projects;
using TaskManager.Domain.Tasks;

namespace TaskManager.Application.Projects;

/// <summary>ステータス（REQ-STS-01, 02）。</summary>
public sealed class WorkflowStatusService(
    IAppDbContext db, ICurrentUser currentUser, ProjectAccessGuard guard, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<WorkflowStatusResponse>> ListAsync(string projectKey, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.ViewProject, cancellationToken);
        return await ListInternalAsync(access.ProjectId, cancellationToken);
    }

    public async Task<WorkflowStatusResponse> CreateAsync(string projectKey, CreateWorkflowStatusRequest request, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.ManageStatuses, cancellationToken);
        var statuses = await db.WorkflowStatuses.Where(s => s.ProjectId == access.ProjectId).ToListAsync(cancellationToken);
        if (statuses.Count >= WorkflowStatus.MaxPerProject)
        {
            throw Errors.Conflict(ErrorCodes.StatusLimitReached, "ステータスは 20 個までです。");
        }

        var status = Wrap(() => WorkflowStatus.Create(
            access.ProjectId, request.Name, request.Category!.Value, request.Color!.Value, statuses.Count + 1, isDefault: false));
        EnsureUniqueName(statuses, status.Name, null);
        db.WorkflowStatuses.Add(status);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(status, 0);
    }

    public async Task<WorkflowStatusResponse> UpdateAsync(
        string projectKey, Guid statusId, UpdateWorkflowStatusRequest request, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.ManageStatuses, cancellationToken);
        var statuses = await db.WorkflowStatuses.Where(s => s.ProjectId == access.ProjectId).ToListAsync(cancellationToken);
        var status = statuses.SingleOrDefault(s => s.Id == statusId) ?? throw Errors.NotFound();

        Wrap(() =>
        {
            if (request.IsSpecified(nameof(request.Name)))
            {
                var name = WorkflowStatus.NormalizeName(request.Name);
                EnsureUniqueName(statuses, name, status.Id);
                status.Rename(name);
            }

            if (request.IsSpecified(nameof(request.Color)) && request.Color is { } color)
            {
                status.ChangeColor(color);
            }

            if (request.IsSpecified(nameof(request.Category)) && request.Category is { } category)
            {
                status.ChangeCategory(category);
            }

            if (request.IsSpecified(nameof(request.IsDefault)))
            {
                if (request.IsDefault != true)
                {
                    throw new DomainException("validation.failed",
                        "既定を外すには、別のステータスを既定にしてください。", "isDefault");
                }

                status.MarkAsDefault();
                foreach (var other in statuses.Where(s => s.Id != status.Id && s.IsDefault))
                {
                    other.UnmarkDefault();
                }
            }

            return true;
        });

        await db.SaveChangesAsync(cancellationToken);
        var count = await db.TaskItems.CountAsync(t => t.StatusId == status.Id, cancellationToken);
        return ToResponse(status, count);
    }

    public async Task<IReadOnlyList<WorkflowStatusResponse>> ReorderAsync(
        string projectKey, ReorderWorkflowStatusesRequest request, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.ManageStatuses, cancellationToken);
        var statuses = await db.WorkflowStatuses.Where(s => s.ProjectId == access.ProjectId).ToListAsync(cancellationToken);
        var ids = request.StatusIds;
        if (ids.Count != statuses.Count || ids.Distinct().Count() != ids.Count || !statuses.All(s => ids.Contains(s.Id)))
        {
            throw Errors.Validation("statusIds", "すべてのステータスを 1 回ずつ並べてください。");
        }

        for (var i = 0; i < ids.Count; i++)
        {
            statuses.Single(s => s.Id == ids[i]).MoveTo(i + 1);
        }

        await db.SaveChangesAsync(cancellationToken);
        return await ListInternalAsync(access.ProjectId, cancellationToken);
    }

    public async Task DeleteAsync(string projectKey, Guid statusId, Guid? moveTasksToStatusId, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(projectKey, ProjectPermission.ManageStatuses, cancellationToken);
        var statuses = await db.WorkflowStatuses.Where(s => s.ProjectId == access.ProjectId).ToListAsync(cancellationToken);
        var status = statuses.SingleOrDefault(s => s.Id == statusId) ?? throw Errors.NotFound();
        if (statuses.Count == 1)
        {
            throw Errors.Conflict(ErrorCodes.StatusLastOne, "最後のステータスは削除できません。");
        }

        if (status.IsDefault)
        {
            throw Errors.Conflict(ErrorCodes.StatusIsDefault,
                "既定のステータスは削除できません。先に別のステータスを既定にしてください。");
        }

        var tasks = await db.TaskItems.Where(t => t.StatusId == status.Id).ToListAsync(cancellationToken);
        if (tasks.Count > 0)
        {
            if (moveTasksToStatusId is null)
            {
                throw Errors.Conflict(ErrorCodes.StatusInUse, "このステータスのタスクがあります。移動先のステータスを選んでください。");
            }

            var target = statuses.SingleOrDefault(s => s.Id == moveTasksToStatusId && s.Id != status.Id)
                ?? throw Errors.Validation("moveTasksToStatusId", "移動先には、同じプロジェクトの別のステータスを選んでください。");
            var now = timeProvider.UtcNow();
            foreach (var task in tasks)
            {
                task.ChangeStatus(target, status.Category, now);
                db.TaskHistoryEntries.Add(TaskHistoryEntry.Create(
                    task.Id, TaskHistoryKind.StatusChanged, currentUser.UserId, status.Name, target.Name, now));
            }
        }

        db.WorkflowStatuses.Remove(status);
        var order = 1;
        foreach (var s in statuses.Where(s => s.Id != status.Id).OrderBy(s => s.SortOrder))
        {
            s.MoveTo(order++);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static void EnsureUniqueName(IEnumerable<WorkflowStatus> statuses, string name, Guid? exceptId)
    {
        if (statuses.Any(s => s.Id != exceptId && string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw Errors.Conflict(ErrorCodes.StatusNameTaken, "同じ名前のステータスが既にあります。");
        }
    }

    private static T Wrap<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch (DomainException ex)
        {
            throw Errors.FromDomain(ex);
        }
    }

    private async Task<IReadOnlyList<WorkflowStatusResponse>> ListInternalAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var statuses = await db.WorkflowStatuses.AsNoTracking().Where(s => s.ProjectId == projectId)
            .OrderBy(s => s.SortOrder).ToListAsync(cancellationToken);
        var counts = await db.TaskItems.AsNoTracking().Where(t => t.ProjectId == projectId)
            .GroupBy(t => t.StatusId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        return statuses.Select(s => ToResponse(s, counts.GetValueOrDefault(s.Id))).ToList();
    }

    internal static WorkflowStatusResponse ToResponse(WorkflowStatus s, int taskCount) =>
        new(s.Id, s.Name, s.Category, s.Color, s.SortOrder, s.IsDefault, taskCount);
}
