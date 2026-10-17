using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TaskManager.Api.Controllers;

/// <summary>
/// 環境確認用のエンドポイント（業務機能ではない）。docs/design/api-design.md #61。
/// </summary>
[ApiController]
[Route("api/v1/system")]
[Tags("System")]
public sealed class SystemController : ControllerBase
{
    /// <summary>API の名前と版を返す。Swagger UI と OpenAPI 文書の動作確認に使う。</summary>
    [HttpGet("info")]
    [AllowAnonymous]
    [EndpointName("GetSystemInfo")]
    [EndpointSummary("API の名前と版を取得する")]
    [ProducesResponseType<SystemInfoResponse>(StatusCodes.Status200OK)]
    public ActionResult<SystemInfoResponse> GetSystemInfo()
    {
        var version = typeof(SystemController).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        // ビルドメタデータ（+コミットハッシュ）は外部に出さない
        var plusIndex = version.IndexOf('+', StringComparison.Ordinal);
        return Ok(new SystemInfoResponse("TaskManager", plusIndex >= 0 ? version[..plusIndex] : version));
    }
}

/// <summary>API の名前と版。</summary>
/// <param name="Name">システム名。</param>
/// <param name="Version">API の版。</param>
public sealed record SystemInfoResponse(string Name, string Version);
