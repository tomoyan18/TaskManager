using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;
using TaskManager.Api.Security;
using TaskManager.Application.Common;
using TaskManager.Application.Tasks;

namespace TaskManager.Api.Controllers;

[Route("api/v1/projects/{projectKey}/tasks")]
[Tags("Tasks")]
public sealed class TasksController(
    TaskItemService service, TaskScheduleService scheduleService, TaskStatusSummaryService statusSummaryService) : ApiControllerBase
{
    /// <summary>タスクの一覧。既定は未完了（未着手・進行中）のみ、更新の新しい順、50 件。</summary>
    [HttpGet]
    [RequiredPermission("ViewProject", "プロジェクトの閲覧者以上")]
    [EndpointName("ListTasks")]
    [EndpointSummary("タスクの一覧を取得する")]
    [ProducesResponseType<PagedResponse<TaskSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public Task<PagedResponse<TaskSummaryResponse>> ListTasks(
        string projectKey, [FromQuery] TaskListQuery query, CancellationToken cancellationToken) =>
        service.ListAsync(projectKey, query, cancellationToken);

    /// <summary>
    /// ガントチャートの日程。表示期間（from〜to、366 日以内）と重なるタスク（最大 500 件）と、
    /// 開始日・期限がどちらもないタスク（件数と先頭 50 件）を返す。既定は未完了のみ。日程の変更は UpdateTask で行う。
    /// </summary>
    [HttpGet("schedule")]
    [RequiredPermission("ViewProject", "プロジェクトの閲覧者以上")]
    [EndpointName("GetTaskSchedule")]
    [EndpointSummary("ガントチャートの日程を取得する")]
    [ProducesResponseType<TaskScheduleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public Task<TaskScheduleResponse> GetTaskSchedule(
        string projectKey,
        [FromQuery, Required(ErrorMessage = "表示期間の開始日を入力してください。")] DateOnly? from,
        [FromQuery, Required(ErrorMessage = "表示期間の終了日を入力してください。")] DateOnly? to,
        [FromQuery] bool? includeClosed,
        CancellationToken cancellationToken) =>
        scheduleService.GetAsync(projectKey, new TaskScheduleQuery { From = from, To = to, IncludeClosed = includeClosed }, cancellationToken);

    /// <summary>
    /// プロジェクトの全タスクのステータス別の件数と割合（円グラフ用）。0 件のステータスも並び順で返す。
    /// 割合は小数第 1 位で、合計が 100.0 になるよう丸める（最大剰余法）。
    /// </summary>
    [HttpGet("status-summary")]
    [RequiredPermission("ViewProject", "プロジェクトの閲覧者以上")]
    [EndpointName("GetTaskStatusSummary")]
    [EndpointSummary("ステータス別のタスク件数と割合を取得する")]
    [ProducesResponseType<TaskStatusSummaryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public Task<TaskStatusSummaryResponse> GetTaskStatusSummary(string projectKey, CancellationToken cancellationToken) =>
        statusSummaryService.GetForProjectAsync(projectKey, cancellationToken);

    /// <summary>タスクを作成する。必須はタイトルのみ。番号はプロジェクト内で自動採番される。</summary>
    [HttpPost]
    [RequiredPermission("CreateTasks", "プロジェクトの編集者以上")]
    [EndpointName("CreateTask")]
    [EndpointSummary("タスクを作成する")]
    [ProducesResponseType<TaskDetailResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public async Task<ActionResult<TaskDetailResponse>> CreateTask(
        string projectKey, CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var created = await service.CreateAsync(projectKey, request, cancellationToken);
        return CreatedAtAction(nameof(GetTask), new { projectKey, taskNumber = created.Number }, created);
    }

    [HttpGet("{taskNumber:int:min(1)}")]
    [RequiredPermission("ViewProject", "プロジェクトの閲覧者以上")]
    [EndpointName("GetTask")]
    [EndpointSummary("タスクの詳細を取得する")]
    [ProducesResponseType<TaskDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public Task<TaskDetailResponse> GetTask(string projectKey, int taskNumber, CancellationToken cancellationToken) =>
        service.GetAsync(projectKey, taskNumber, cancellationToken);

    /// <summary>タスクを変更する。送った項目だけを変更し、null で値を消す。tagIds は置き換え。version が必須。</summary>
    [HttpPatch("{taskNumber:int:min(1)}")]
    [RequiredPermission("EditTasks", "プロジェクトの編集者以上")]
    [EndpointName("UpdateTask")]
    [EndpointSummary("タスクを変更する")]
    [ProducesResponseType<TaskDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public Task<TaskDetailResponse> UpdateTask(
        string projectKey, int taskNumber, UpdateTaskRequest request, CancellationToken cancellationToken) =>
        service.UpdateAsync(projectKey, taskNumber, request, cancellationToken);

    /// <summary>タスクを削除する（元に戻せない）。サブタスクがある場合は includeSubtasks=true が必要。</summary>
    [HttpDelete("{taskNumber:int:min(1)}")]
    [RequiredPermission("DeleteAnyTask", "プロジェクトの管理者、または自分が作成したタスクの編集者")]
    [EndpointName("DeleteTask")]
    [EndpointSummary("タスクを削除する")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public async Task<IActionResult> DeleteTask(
        string projectKey, int taskNumber, [FromQuery] bool? includeSubtasks, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(projectKey, taskNumber, includeSubtasks == true, cancellationToken);
        return NoContent();
    }
}

[Route("api/v1/projects/{projectKey}/tasks/{taskNumber:int:min(1)}/history")]
[Tags("TaskHistory")]
public sealed class TaskHistoryController(TaskHistoryService service) : ApiControllerBase
{
    [HttpGet]
    [RequiredPermission("ViewProject", "プロジェクトの閲覧者以上")]
    [EndpointName("ListTaskHistory")]
    [EndpointSummary("タスクの変更履歴を取得する（新しい順）")]
    [ProducesResponseType<PagedResponse<TaskHistoryEntryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public Task<PagedResponse<TaskHistoryEntryResponse>> ListTaskHistory(
        string projectKey, int taskNumber, [FromQuery, Range(1, int.MaxValue)] int? page, [FromQuery, Range(1, 100)] int? pageSize,
        CancellationToken cancellationToken) =>
        service.ListAsync(projectKey, taskNumber, page, pageSize, cancellationToken);
}

[Route("api/v1/projects/{projectKey}/tasks/{taskNumber:int:min(1)}/comments")]
[Tags("TaskComments")]
public sealed class TaskCommentsController(TaskCommentService service) : ApiControllerBase
{
    [HttpGet]
    [RequiredPermission("ViewProject", "プロジェクトの閲覧者以上")]
    [EndpointName("ListTaskComments")]
    [EndpointSummary("コメントの一覧を取得する（古い順）")]
    [ProducesResponseType<PagedResponse<TaskCommentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public Task<PagedResponse<TaskCommentResponse>> ListTaskComments(
        string projectKey, int taskNumber, [FromQuery, Range(1, int.MaxValue)] int? page, [FromQuery, Range(1, 100)] int? pageSize,
        CancellationToken cancellationToken) =>
        service.ListAsync(projectKey, taskNumber, page, pageSize, cancellationToken);

    [HttpPost]
    [RequiredPermission("Comment", "プロジェクトの編集者以上")]
    [EndpointName("CreateTaskComment")]
    [EndpointSummary("コメントを投稿する")]
    [ProducesResponseType<TaskCommentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public async Task<ActionResult<TaskCommentResponse>> CreateTaskComment(
        string projectKey, int taskNumber, CreateTaskCommentRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateAsync(projectKey, taskNumber, request, cancellationToken));

    [HttpPatch("{commentId:guid}")]
    [RequiredPermission("Comment", "投稿者本人（編集者以上）")]
    [EndpointName("UpdateTaskComment")]
    [EndpointSummary("コメントを編集する")]
    [ProducesResponseType<TaskCommentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public Task<TaskCommentResponse> UpdateTaskComment(
        string projectKey, int taskNumber, Guid commentId, UpdateTaskCommentRequest request, CancellationToken cancellationToken) =>
        service.UpdateAsync(projectKey, taskNumber, commentId, request, cancellationToken);

    [HttpDelete("{commentId:guid}")]
    [RequiredPermission("DeleteAnyComment", "投稿者本人（編集者以上）、またはプロジェクトの管理者")]
    [EndpointName("DeleteTaskComment")]
    [EndpointSummary("コメントを削除する")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public async Task<IActionResult> DeleteTaskComment(string projectKey, int taskNumber, Guid commentId, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(projectKey, taskNumber, commentId, cancellationToken);
        return NoContent();
    }
}

[Route("api/v1/projects/{projectKey}/tasks/{taskNumber:int:min(1)}/attachments")]
[Tags("TaskAttachments")]
public sealed class TaskAttachmentsController(TaskAttachmentService service) : ApiControllerBase
{
    /// <summary>添付の上限（ファイル 10MB + マルチパートの余裕）。</summary>
    public const long RequestLimitBytes = 11 * 1024 * 1024;

    private static readonly HashSet<string> TextContentTypes = new(StringComparer.Ordinal) { "text/plain", "text/csv", "text/markdown" };

    [HttpGet]
    [RequiredPermission("ViewProject", "プロジェクトの閲覧者以上")]
    [EndpointName("ListTaskAttachments")]
    [EndpointSummary("添付ファイルの一覧を取得する")]
    [ProducesResponseType<IReadOnlyList<TaskAttachmentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public Task<IReadOnlyList<TaskAttachmentResponse>> ListTaskAttachments(string projectKey, int taskNumber, CancellationToken cancellationToken) =>
        service.ListAsync(projectKey, taskNumber, cancellationToken);

    /// <summary>
    /// ファイルを添付する（multipart/form-data の file パート 1 件、10MB まで）。
    /// 添付できる形式: PDF、PNG、JPEG、GIF、WebP、TXT、CSV、MD、DOCX、XLSX、PPTX、ZIP。
    /// </summary>
    [HttpPost]
    [RequiredPermission("UploadAttachments", "プロジェクトの編集者以上")]
    [EnableRateLimiting(RateLimitPolicies.Upload)]
    [RequestSizeLimit(RequestLimitBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = RequestLimitBytes)]
    [Consumes("multipart/form-data")]
    [EndpointName("UploadTaskAttachment")]
    [EndpointSummary("ファイルを添付する")]
    [ProducesResponseType<TaskAttachmentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType, ProblemJson)]
    public async Task<ActionResult<TaskAttachmentResponse>> UploadTaskAttachment(
        string projectKey, int taskNumber, [Required] IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var created = await service.UploadAsync(projectKey, taskNumber, file.FileName, stream, file.Length, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>添付ファイルをダウンロードする（常にダウンロードとして返す）。</summary>
    [HttpGet("{attachmentId:guid}/content")]
    [RequiredPermission("ViewProject", "プロジェクトの閲覧者以上")]
    [EndpointName("DownloadTaskAttachment")]
    [EndpointSummary("添付ファイルをダウンロードする")]
    [ProducesResponseType<FileStreamResult>(StatusCodes.Status200OK, "application/octet-stream")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<IActionResult> DownloadTaskAttachment(
        string projectKey, int taskNumber, Guid attachmentId, CancellationToken cancellationToken)
    {
        var content = await service.OpenReadAsync(projectKey, taskNumber, attachmentId, cancellationToken);
        var contentType = TextContentTypes.Contains(content.ContentType) ? "text/plain; charset=utf-8" : content.ContentType;
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        var disposition = new ContentDispositionHeaderValue("attachment");
        disposition.SetHttpFileName(content.FileName);
        Response.Headers.ContentDisposition = disposition.ToString();
        return new FileStreamResult(content.Content, contentType);
    }

    [HttpDelete("{attachmentId:guid}")]
    [RequiredPermission("DeleteAnyAttachment", "登録者本人（編集者以上）、またはプロジェクトの管理者")]
    [EndpointName("DeleteTaskAttachment")]
    [EndpointSummary("添付ファイルを削除する")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public async Task<IActionResult> DeleteTaskAttachment(
        string projectKey, int taskNumber, Guid attachmentId, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(projectKey, taskNumber, attachmentId, cancellationToken);
        return NoContent();
    }
}
