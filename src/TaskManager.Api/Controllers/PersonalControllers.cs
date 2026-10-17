using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaskManager.Api.Security;
using TaskManager.Application.Common;
using TaskManager.Application.Notices;
using TaskManager.Application.Tasks;

namespace TaskManager.Api.Controllers;

[Route("api/v1/me/tasks")]
[Tags("MyTasks")]
public sealed class MyTasksController(MyTasksService service, TaskStatusSummaryService statusSummaryService) : ApiControllerBase
{
    /// <summary>
    /// 参加中（アーカイブ以外）のプロジェクトで自分が担当の未完了タスクを、期限で区分して返す
    /// （期限切れ / 今日 / 今週 / それ以降 / 期限なし。利用者のタイムゾーン基準）。
    /// </summary>
    [HttpGet]
    [EndpointName("GetMyTasks")]
    [EndpointSummary("自分のタスクを取得する")]
    [ProducesResponseType<MyTasksResponse>(StatusCodes.Status200OK)]
    public Task<MyTasksResponse> GetMyTasks([FromQuery] bool? includeRecentlyCompleted, CancellationToken cancellationToken) =>
        service.GetAsync(includeRecentlyCompleted == true, cancellationToken);

    /// <summary>
    /// 自分が担当のタスクのステータス別の件数と割合（円グラフ用。完了・中止を含む）。
    /// projectKey を指定しない場合は、参加中（アーカイブ以外）の全プロジェクトを区分別（未着手・進行中・完了・中止）に、
    /// 指定した場合はそのプロジェクトのステータス別に集計する。
    /// </summary>
    [HttpGet("status-summary")]
    [EndpointName("GetMyTaskStatusSummary")]
    [EndpointSummary("自分のタスクのステータス別の件数と割合を取得する")]
    [ProducesResponseType<TaskStatusSummaryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public Task<TaskStatusSummaryResponse> GetMyTaskStatusSummary(
        [FromQuery, RegularExpression(Domain.Common.ProjectKey.Pattern, ErrorMessage = ValidationMessages.ProjectKey)] string? projectKey, CancellationToken cancellationToken) =>
        statusSummaryService.GetForMeAsync(projectKey, cancellationToken);
}

[Route("api/v1/me/notices")]
[Tags("Notices")]
public sealed class NoticesController(NoticeService service) : ApiControllerBase
{
    [HttpGet]
    [EndpointName("ListMyNotices")]
    [EndpointSummary("自分宛てのお知らせを取得する（新しい順）")]
    [ProducesResponseType<PagedResponse<NoticeResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    public Task<PagedResponse<NoticeResponse>> ListMyNotices(
        [FromQuery] bool? unreadOnly, [FromQuery, Range(1, int.MaxValue)] int? page, [FromQuery, Range(1, 100)] int? pageSize,
        CancellationToken cancellationToken) =>
        service.ListAsync(unreadOnly == true, page, pageSize, cancellationToken);

    [HttpGet("unread-count")]
    [EndpointName("GetMyUnreadNoticeCount")]
    [EndpointSummary("未読のお知らせの件数を取得する")]
    [ProducesResponseType<UnreadNoticeCountResponse>(StatusCodes.Status200OK)]
    public Task<UnreadNoticeCountResponse> GetMyUnreadNoticeCount(CancellationToken cancellationToken) =>
        service.CountUnreadAsync(cancellationToken);

    [HttpPost("{noticeId:guid}/read")]
    [EndpointName("MarkNoticeRead")]
    [EndpointSummary("お知らせを既読にする")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<IActionResult> MarkNoticeRead(Guid noticeId, CancellationToken cancellationToken)
    {
        await service.MarkReadAsync(noticeId, cancellationToken);
        return NoContent();
    }

    [HttpPost("read-all")]
    [EndpointName("MarkAllNoticesRead")]
    [EndpointSummary("すべてのお知らせを既読にする")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllNoticesRead(CancellationToken cancellationToken)
    {
        await service.MarkAllReadAsync(cancellationToken);
        return NoContent();
    }
}

[Route("api/v1/search")]
[Tags("Search")]
public sealed class SearchController(TaskSearchService service) : ApiControllerBase
{
    /// <summary>参加中のプロジェクトからタスクを検索する。「SALES-12」形式ならそのタスクを先頭に返す（最大 20 件）。</summary>
    [HttpGet("tasks")]
    [EnableRateLimiting(RateLimitPolicies.Search)]
    [EndpointName("SearchTasks")]
    [EndpointSummary("タスクを検索する")]
    [ProducesResponseType<IReadOnlyList<TaskSearchResultResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    public Task<IReadOnlyList<TaskSearchResultResponse>> SearchTasks(
        [FromQuery, Required, StringLength(100, MinimumLength = 1)] string q, CancellationToken cancellationToken) =>
        service.SearchAsync(q, cancellationToken);
}
