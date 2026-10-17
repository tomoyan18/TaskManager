using Microsoft.AspNetCore.Mvc;

namespace TaskManager.Api.Controllers;

/// <summary>
/// すべての API Controller の基底。応答は JSON。401・403(パスワード変更必須)・429・500 の共通エラーは
/// OpenAPI の変換処理（OpenApiSetup）で全操作に付与する。
/// </summary>
[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected const string ProblemJson = "application/problem+json";
}
