using System.Globalization;
using TaskManager.Domain.Common;

namespace TaskManager.Application.Tasks;

/// <summary>変更履歴・お知らせ・集計に使う表示用の日本語。</summary>
public static class TaskLabels
{
    public static string Priority(TaskPriority priority) => priority switch
    {
        TaskPriority.Low => "低",
        TaskPriority.Normal => "中",
        TaskPriority.High => "高",
        TaskPriority.Urgent => "至急",
        _ => priority.ToString(),
    };

    /// <summary>ステータスの区分（自分の担当の区分別集計で使う。Frontend の CATEGORY_LABELS と同じ）。</summary>
    public static string Category(StatusCategory category) => category switch
    {
        StatusCategory.NotStarted => "未着手",
        StatusCategory.InProgress => "進行中",
        StatusCategory.Done => "完了",
        StatusCategory.Canceled => "中止",
        _ => category.ToString(),
    };

    public static string? Date(DateOnly? date) =>
        date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
