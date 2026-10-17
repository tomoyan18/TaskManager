using Microsoft.AspNetCore.Mvc;
using TaskManager.Api.Security;
using TaskManager.Application.Users;

namespace TaskManager.Api.Controllers;

[Route("api/v1/me")]
[Tags("Me")]
public sealed class MeController(MyAccountService service) : ApiControllerBase
{
    /// <summary>ログイン中の利用者の情報を取得する。</summary>
    [HttpGet]
    [AllowWhenPasswordChangeRequired]
    [EndpointName("GetMe")]
    [EndpointSummary("自分の情報を取得する")]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    public Task<CurrentUserResponse> GetMe(CancellationToken cancellationToken) => service.GetAsync(cancellationToken);

    /// <summary>表示名とタイムゾーンを変更する。</summary>
    [HttpPatch]
    [EndpointName("UpdateMe")]
    [EndpointSummary("自分の情報を変更する")]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    public Task<CurrentUserResponse> UpdateMe(UpdateMeRequest request, CancellationToken cancellationToken) =>
        service.UpdateProfileAsync(request, cancellationToken);
}
