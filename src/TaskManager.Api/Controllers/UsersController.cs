using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManager.Api.Security;
using TaskManager.Application.Common;
using TaskManager.Application.Users;

namespace TaskManager.Api.Controllers;

/// <summary>利用者管理（システム管理者のみ）。</summary>
[Route("api/v1/users")]
[Tags("Users")]
[Authorize(Policy = Policies.SystemAdministrator)]
public sealed class UsersController(UserAdministrationService service) : ApiControllerBase
{
    private const string Admin = "システム管理者";

    /// <summary>利用者の一覧。sort: displayName（既定）, email, -lastLoginAt, createdAt</summary>
    [HttpGet]
    [RequiredPermission("SystemAdministrator", Admin)]
    [EndpointName("ListUsers")]
    [EndpointSummary("利用者の一覧を取得する")]
    [ProducesResponseType<PagedResponse<UserAdminSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    public Task<PagedResponse<UserAdminSummaryResponse>> ListUsers(
        [FromQuery, System.ComponentModel.DataAnnotations.StringLength(100)] string? q, [FromQuery] UserStatusFilter? status, [FromQuery] string? sort,
        [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        service.ListAsync(q, status, sort, PageRequest.Of(page, pageSize, 50), cancellationToken);

    /// <summary>利用者を発行する。一時パスワードはこの応答でのみ返す。</summary>
    [HttpPost]
    [RequiredPermission("SystemAdministrator", Admin)]
    [EndpointName("CreateUser")]
    [EndpointSummary("利用者を発行する")]
    [ProducesResponseType<CreatedUserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public async Task<ActionResult<CreatedUserResponse>> CreateUser(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var created = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetUser), new { userId = created.User.Id }, created);
    }

    [HttpGet("{userId:guid}")]
    [RequiredPermission("SystemAdministrator", Admin)]
    [EndpointName("GetUser")]
    [EndpointSummary("利用者の詳細を取得する")]
    [ProducesResponseType<UserAdminDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public Task<UserAdminDetailResponse> GetUser(Guid userId, CancellationToken cancellationToken) =>
        service.GetAsync(userId, cancellationToken);

    [HttpPatch("{userId:guid}")]
    [RequiredPermission("SystemAdministrator", Admin)]
    [EndpointName("UpdateUser")]
    [EndpointSummary("利用者の表示名・システムロールを変更する")]
    [ProducesResponseType<UserAdminDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public Task<UserAdminDetailResponse> UpdateUser(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken) =>
        service.UpdateAsync(userId, request, cancellationToken);

    [HttpPost("{userId:guid}/deactivate")]
    [RequiredPermission("SystemAdministrator", Admin)]
    [EndpointName("DeactivateUser")]
    [EndpointSummary("利用者を利用停止にする")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public async Task<IActionResult> DeactivateUser(Guid userId, [FromServices] SecurityStampValidator stamps, CancellationToken cancellationToken)
    {
        await service.DeactivateAsync(userId, cancellationToken);
        stamps.Invalidate(userId);
        return NoContent();
    }

    [HttpPost("{userId:guid}/activate")]
    [RequiredPermission("SystemAdministrator", Admin)]
    [EndpointName("ActivateUser")]
    [EndpointSummary("利用停止を解除する")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<IActionResult> ActivateUser(Guid userId, [FromServices] SecurityStampValidator stamps, CancellationToken cancellationToken)
    {
        await service.ActivateAsync(userId, cancellationToken);
        stamps.Invalidate(userId);
        return NoContent();
    }

    [HttpPost("{userId:guid}/unlock")]
    [RequiredPermission("SystemAdministrator", Admin)]
    [EndpointName("UnlockUser")]
    [EndpointSummary("ログインのロックを解除する")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<IActionResult> UnlockUser(Guid userId, CancellationToken cancellationToken)
    {
        await service.UnlockAsync(userId, cancellationToken);
        return NoContent();
    }

    /// <summary>一時パスワードを再発行する（この応答でのみ返す）。対象の利用者のログインはすべて無効になる。</summary>
    [HttpPost("{userId:guid}/password-reset")]
    [RequiredPermission("SystemAdministrator", Admin)]
    [EndpointName("ResetUserPassword")]
    [EndpointSummary("一時パスワードを再発行する")]
    [ProducesResponseType<TemporaryPasswordResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<TemporaryPasswordResponse> ResetUserPassword(
        Guid userId, [FromServices] SecurityStampValidator stamps, CancellationToken cancellationToken)
    {
        var result = await service.ResetPasswordAsync(userId, cancellationToken);
        stamps.Invalidate(userId);
        return result;
    }
}

[Route("api/v1/user-directory")]
[Tags("UserDirectory")]
public sealed class UserDirectoryController(UserDirectoryService service) : ApiControllerBase
{
    /// <summary>メンバー追加用に、有効な利用者を表示名・メールで検索する（最大 20 件）。</summary>
    [HttpGet]
    [RequiredPermission("ManageMembers", "いずれかのプロジェクトの管理者、またはシステム管理者")]
    [EndpointName("SearchUserDirectory")]
    [EndpointSummary("利用者を検索する")]
    [ProducesResponseType<IReadOnlyList<UserDirectoryEntry>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    public Task<IReadOnlyList<UserDirectoryEntry>> SearchUserDirectory(
        [FromQuery, System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 1)] string q,
        [FromQuery] string? excludeProjectKey, CancellationToken cancellationToken) =>
        service.SearchAsync(q, excludeProjectKey, cancellationToken);
}
