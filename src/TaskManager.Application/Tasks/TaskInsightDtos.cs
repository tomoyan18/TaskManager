using TaskManager.Domain.Common;

namespace TaskManager.Application.Tasks;

/// <summary>ガントチャートの日程の取得条件（docs/design/api-design.md #62。Api がクエリから組み立てる）。</summary>
public sealed class TaskScheduleQuery
{
    /// <summary>表示期間の開始日（含む）。</summary>
    public DateOnly? From { get; set; }

    /// <summary>表示期間の終了日（含む）。開始日から 366 日以内。</summary>
    public DateOnly? To { get; set; }

    /// <summary>完了・中止の区分も含める（既定は未着手・進行中のみ）。</summary>
    public bool? IncludeClosed { get; set; }
}

/// <summary>ガントチャートの日程（#62）。行は一覧（#41）と同じ形。</summary>
public sealed record TaskScheduleResponse(
    DateOnly From,
    DateOnly To,
    DateOnly Today,
    string TimeZoneId,
    IReadOnlyList<TaskSummaryResponse> Items,
    bool Truncated,
    IReadOnlyList<TaskSummaryResponse> UndatedItems,
    int UndatedCount);

/// <summary>ステータス別の集計の単位。</summary>
public enum StatusSummaryGrouping
{
    /// <summary>プロジェクトのステータスごと。</summary>
    Status,

    /// <summary>ステータスの区分ごと（複数プロジェクトにまたがる自分の担当の集計）。</summary>
    Category,
}

/// <summary>
/// ステータス（または区分）ごとの件数と割合。区分ごとの集計では StatusId は null、Name は区分の日本語名、
/// Color は区分の既定色（domain-model.md §5.2 SR-05）。Percent は小数第 1 位で、全項目の合計は 100.0（合計 0 件を除く）。
/// </summary>
public sealed record StatusCountResponse(
    Guid? StatusId,
    string Name,
    StatusCategory Category,
    StatusColor Color,
    int SortOrder,
    int Count,
    decimal Percent);

/// <summary>ステータス別の件数・割合（#63、#64）。</summary>
public sealed record TaskStatusSummaryResponse(
    StatusSummaryGrouping GroupedBy,
    int TotalCount,
    IReadOnlyList<StatusCountResponse> Items);
