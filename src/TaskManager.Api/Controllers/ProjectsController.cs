using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using TaskManager.Api.Security;
using TaskManager.Application.Common;
using TaskManager.Application.Projects;

namespace TaskManager.Api.Controllers;

[Route("api/v1/projects")]
[Tags("Projects")]
public sealed class ProjectsController(ProjectService service) : ApiControllerBase
{
    /// <summary>
    /// 参加しているプロジェクトの一覧。sort: name（既定）, key, -updatedAt。
    /// scope=all はシステム管理者のみ有効（参加していないプロジェクトは名前・キー・管理者のみ）。
    /// </summary>
    [HttpGet]
    [EndpointName("ListProjects")]
    [EndpointSummary("プロジェクトの一覧を取得する")]
    [ProducesResponseType<PagedResponse<ProjectSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    public Task<PagedResponse<ProjectSummaryResponse>> ListProjects(
        [FromQuery] bool? includeArchived, [FromQuery, StringLength(80)] string? q, [FromQuery] string? sort,
        [FromQuery] string? scope, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        service.ListAsync(includeArchived == true, q, sort, scope == "all", PageRequest.Of(page, pageSize, 50), cancellationToken);

    /// <summary>プロジェクトを作成する。作成者が管理者になり、既定のステータスが作られる。</summary>
    [HttpPost]
    [EndpointName("CreateProject")]
    [EndpointSummary("プロジェクトを作成する")]
    [ProducesResponseType<ProjectDetailResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public async Task<ActionResult<ProjectDetailResponse>> CreateProject(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var created = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetProject), new { projectKey = created.Key }, created);
    }

    [HttpGet("{projectKey}")]
    [RequiredPermission("ViewProject", "プロジェクトの閲覧者以上")]
    [EndpointName("GetProject")]
    [EndpointSummary("プロジェクトの詳細を取得する")]
    [ProducesResponseType<ProjectDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public Task<ProjectDetailResponse> GetProject(string projectKey, CancellationToken cancellationToken) =>
        service.GetAsync(projectKey, cancellationToken);

    [HttpPatch("{projectKey}")]
    [RequiredPermission("EditProjectSettings", "プロジェクトの管理者")]
    [EndpointName("UpdateProject")]
    [EndpointSummary("プロジェクトの名前・説明を変更する")]
    [ProducesResponseType<ProjectDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public Task<ProjectDetailResponse> UpdateProject(string projectKey, UpdateProjectRequest request, CancellationToken cancellationToken) =>
        service.UpdateAsync(projectKey, request, cancellationToken);

    [HttpPost("{projectKey}/archive")]
    [RequiredPermission("ArchiveProject", "プロジェクトの管理者")]
    [EndpointName("ArchiveProject")]
    [EndpointSummary("プロジェクトをアーカイブする")]
    [ProducesResponseType<ProjectDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public Task<ProjectDetailResponse> ArchiveProject(string projectKey, CancellationToken cancellationToken) =>
        service.ArchiveAsync(projectKey, archive: true, cancellationToken);

    [HttpPost("{projectKey}/unarchive")]
    [RequiredPermission("ArchiveProject", "プロジェクトの管理者")]
    [EndpointName("UnarchiveProject")]
    [EndpointSummary("プロジェクトのアーカイブを解除する")]
    [ProducesResponseType<ProjectDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public Task<ProjectDetailResponse> UnarchiveProject(string projectKey, CancellationToken cancellationToken) =>
        service.ArchiveAsync(projectKey, archive: false, cancellationToken);

    /// <summary>アーカイブ済みのプロジェクトを削除する（元に戻せない）。confirmKey にプロジェクトキーを指定する。</summary>
    [HttpDelete("{projectKey}")]
    [RequiredPermission("DeleteProject", "システム管理者")]
    [EndpointName("DeleteProject")]
    [EndpointSummary("プロジェクトを削除する")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public async Task<IActionResult> DeleteProject(string projectKey, [FromQuery, Required] string confirmKey, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(projectKey, confirmKey, cancellationToken);
        return NoContent();
    }
}

[Route("api/v1/projects/{projectKey}/members")]
[Tags("ProjectMembers")]
public sealed class ProjectMembersController(ProjectMembershipService service) : ApiControllerBase
{
    /// <summary>メンバーの一覧（表示名順）。assignable=true で担当者にできる人（編集者以上・利用中）のみ。</summary>
    [HttpGet]
    [RequiredPermission("ViewProject", "プロジェクトの閲覧者以上")]
    [EndpointName("ListProjectMembers")]
    [EndpointSummary("メンバーの一覧を取得する")]
    [ProducesResponseType<IReadOnlyList<ProjectMemberResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public Task<IReadOnlyList<ProjectMemberResponse>> ListProjectMembers(
        string projectKey, [FromQuery] Domain.Common.ProjectRole? role, [FromQuery] bool? assignable, CancellationToken cancellationToken) =>
        service.ListAsync(projectKey, role, assignable == true, cancellationToken);

    [HttpPost]
    [RequiredPermission("ManageMembers", "プロジェクトの管理者、またはシステム管理者")]
    [EndpointName("AddProjectMember")]
    [EndpointSummary("メンバーを追加する")]
    [ProducesResponseType<ProjectMemberResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public async Task<ActionResult<ProjectMemberResponse>> AddProjectMember(
        string projectKey, AddProjectMemberRequest request, CancellationToken cancellationToken)
    {
        var member = await service.AddAsync(projectKey, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, member);
    }

    [HttpPatch("{userId:guid}")]
    [RequiredPermission("ManageMembers", "プロジェクトの管理者、またはシステム管理者")]
    [EndpointName("ChangeProjectMemberRole")]
    [EndpointSummary("メンバーのロールを変更する")]
    [ProducesResponseType<ProjectMemberResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public Task<ProjectMemberResponse> ChangeProjectMemberRole(
        string projectKey, Guid userId, ChangeProjectMemberRoleRequest request, CancellationToken cancellationToken) =>
        service.ChangeRoleAsync(projectKey, userId, request, cancellationToken);

    /// <summary>メンバーを外す（自分の場合は脱退）。外れた人が担当のタスクは担当者なしになる。</summary>
    [HttpDelete("{userId:guid}")]
    [RequiredPermission("ManageMembers", "プロジェクトの管理者、システム管理者、または本人")]
    [EndpointName("RemoveProjectMember")]
    [EndpointSummary("メンバーを外す")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public async Task<IActionResult> RemoveProjectMember(string projectKey, Guid userId, CancellationToken cancellationToken)
    {
        await service.RemoveAsync(projectKey, userId, cancellationToken);
        return NoContent();
    }
}
