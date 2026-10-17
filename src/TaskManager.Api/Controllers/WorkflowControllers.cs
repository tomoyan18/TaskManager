using Microsoft.AspNetCore.Mvc;
using TaskManager.Api.Security;
using TaskManager.Application.Projects;
using TaskManager.Domain.Common;

namespace TaskManager.Api.Controllers;

[Route("api/v1/projects/{projectKey}/statuses")]
[Tags("WorkflowStatuses")]
public sealed class WorkflowStatusesController(WorkflowStatusService service) : ApiControllerBase
{
    [HttpGet]
    [RequiredPermission("ViewProject", "プロジェクトの閲覧者以上")]
    [EndpointName("ListWorkflowStatuses")]
    [EndpointSummary("ステータスの一覧を取得する")]
    [ProducesResponseType<IReadOnlyList<WorkflowStatusResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public Task<IReadOnlyList<WorkflowStatusResponse>> ListWorkflowStatuses(string projectKey, CancellationToken cancellationToken) =>
        service.ListAsync(projectKey, cancellationToken);

    [HttpPost]
    [RequiredPermission("ManageStatuses", "プロジェクトの管理者")]
    [EndpointName("CreateWorkflowStatus")]
    [EndpointSummary("ステータスを追加する")]
    [ProducesResponseType<WorkflowStatusResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public async Task<ActionResult<WorkflowStatusResponse>> CreateWorkflowStatus(
        string projectKey, CreateWorkflowStatusRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateAsync(projectKey, request, cancellationToken));

    [HttpPatch("{statusId:guid}")]
    [RequiredPermission("ManageStatuses", "プロジェクトの管理者")]
    [EndpointName("UpdateWorkflowStatus")]
    [EndpointSummary("ステータスを変更する")]
    [ProducesResponseType<WorkflowStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public Task<WorkflowStatusResponse> UpdateWorkflowStatus(
        string projectKey, Guid statusId, UpdateWorkflowStatusRequest request, CancellationToken cancellationToken) =>
        service.UpdateAsync(projectKey, statusId, request, cancellationToken);

    /// <summary>ステータスの並び順を変更する（すべてのステータスをちょうど 1 回ずつ指定する）。</summary>
    [HttpPut("order")]
    [RequiredPermission("ManageStatuses", "プロジェクトの管理者")]
    [EndpointName("ReorderWorkflowStatuses")]
    [EndpointSummary("ステータスを並べ替える")]
    [ProducesResponseType<IReadOnlyList<WorkflowStatusResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public Task<IReadOnlyList<WorkflowStatusResponse>> ReorderWorkflowStatuses(
        string projectKey, ReorderWorkflowStatusesRequest request, CancellationToken cancellationToken) =>
        service.ReorderAsync(projectKey, request, cancellationToken);

    /// <summary>ステータスを削除する。そのステータスのタスクがある場合は moveTasksToStatusId が必要。</summary>
    [HttpDelete("{statusId:guid}")]
    [RequiredPermission("ManageStatuses", "プロジェクトの管理者")]
    [EndpointName("DeleteWorkflowStatus")]
    [EndpointSummary("ステータスを削除する")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public async Task<IActionResult> DeleteWorkflowStatus(
        string projectKey, Guid statusId, [FromQuery] Guid? moveTasksToStatusId, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(projectKey, statusId, moveTasksToStatusId, cancellationToken);
        return NoContent();
    }
}

[Route("api/v1/projects/{projectKey}/tags")]
[Tags("Tags")]
public sealed class TagsController(TagService service) : ApiControllerBase
{
    [HttpGet]
    [RequiredPermission("ViewProject", "プロジェクトの閲覧者以上")]
    [EndpointName("ListTags")]
    [EndpointSummary("タグの一覧を取得する")]
    [ProducesResponseType<IReadOnlyList<TagResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public Task<IReadOnlyList<TagResponse>> ListTags(string projectKey, CancellationToken cancellationToken) =>
        service.ListAsync(projectKey, cancellationToken);

    [HttpPost]
    [RequiredPermission("ManageTags", "プロジェクトの編集者以上")]
    [EndpointName("CreateTag")]
    [EndpointSummary("タグを追加する")]
    [ProducesResponseType<TagResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public async Task<ActionResult<TagResponse>> CreateTag(string projectKey, CreateTagRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateAsync(projectKey, request, cancellationToken));

    [HttpPatch("{tagId:guid}")]
    [RequiredPermission("ManageTags", "プロジェクトの編集者以上")]
    [EndpointName("UpdateTag")]
    [EndpointSummary("タグを変更する")]
    [ProducesResponseType<TagResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public Task<TagResponse> UpdateTag(string projectKey, Guid tagId, UpdateTagRequest request, CancellationToken cancellationToken) =>
        service.UpdateAsync(projectKey, tagId, request, cancellationToken);

    [HttpDelete("{tagId:guid}")]
    [RequiredPermission("ManageTags", "プロジェクトの編集者以上")]
    [EndpointName("DeleteTag")]
    [EndpointSummary("タグを削除する")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public async Task<IActionResult> DeleteTag(string projectKey, Guid tagId, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(projectKey, tagId, cancellationToken);
        return NoContent();
    }
}

[Route("api/v1/projects/{projectKey}/milestones")]
[Tags("Milestones")]
public sealed class MilestonesController(MilestoneService service) : ApiControllerBase
{
    [HttpGet]
    [RequiredPermission("ViewProject", "プロジェクトの閲覧者以上")]
    [EndpointName("ListMilestones")]
    [EndpointSummary("マイルストーンの一覧を取得する")]
    [ProducesResponseType<IReadOnlyList<MilestoneSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public Task<IReadOnlyList<MilestoneSummaryResponse>> ListMilestones(
        string projectKey, [FromQuery] MilestoneState? state, CancellationToken cancellationToken) =>
        service.ListAsync(projectKey, state, cancellationToken);

    [HttpPost]
    [RequiredPermission("ManageMilestones", "プロジェクトの編集者以上")]
    [EndpointName("CreateMilestone")]
    [EndpointSummary("マイルストーンを作成する")]
    [ProducesResponseType<MilestoneDetailResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public async Task<ActionResult<MilestoneDetailResponse>> CreateMilestone(
        string projectKey, CreateMilestoneRequest request, CancellationToken cancellationToken)
    {
        var created = await service.CreateAsync(projectKey, request, cancellationToken);
        return CreatedAtAction(nameof(GetMilestone), new { projectKey, milestoneId = created.Id }, created);
    }

    [HttpGet("{milestoneId:guid}")]
    [RequiredPermission("ViewProject", "プロジェクトの閲覧者以上")]
    [EndpointName("GetMilestone")]
    [EndpointSummary("マイルストーンの詳細を取得する")]
    [ProducesResponseType<MilestoneDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public Task<MilestoneDetailResponse> GetMilestone(string projectKey, Guid milestoneId, CancellationToken cancellationToken) =>
        service.GetAsync(projectKey, milestoneId, cancellationToken);

    [HttpPatch("{milestoneId:guid}")]
    [RequiredPermission("ManageMilestones", "プロジェクトの編集者以上")]
    [EndpointName("UpdateMilestone")]
    [EndpointSummary("マイルストーンを変更する")]
    [ProducesResponseType<MilestoneDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public Task<MilestoneDetailResponse> UpdateMilestone(
        string projectKey, Guid milestoneId, UpdateMilestoneRequest request, CancellationToken cancellationToken) =>
        service.UpdateAsync(projectKey, milestoneId, request, cancellationToken);

    /// <summary>マイルストーンを削除する。タスクは残り、マイルストーンとの関連だけが外れる。</summary>
    [HttpDelete("{milestoneId:guid}")]
    [RequiredPermission("ManageMilestones", "プロジェクトの編集者以上")]
    [EndpointName("DeleteMilestone")]
    [EndpointSummary("マイルストーンを削除する")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public async Task<IActionResult> DeleteMilestone(string projectKey, Guid milestoneId, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(projectKey, milestoneId, cancellationToken);
        return NoContent();
    }
}
