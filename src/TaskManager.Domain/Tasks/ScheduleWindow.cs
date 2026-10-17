using TaskManager.Domain.Common;

namespace TaskManager.Domain.Tasks;

/// <summary>
/// ガントチャートの表示期間（開始日〜終了日、両端を含む）。domain-model.md §5.1（SC-01〜SC-04）。
/// タスクの項目（開始日・期限）は変更せず、表示のための期間と重なりの規則だけを持つ。
/// </summary>
public readonly record struct ScheduleWindow
{
    /// <summary>表示できる最大の日数（両端を含む）。</summary>
    public const int MaxDays = 366;

    private ScheduleWindow(DateOnly from, DateOnly to)
    {
        From = from;
        To = to;
    }

    public DateOnly From { get; }

    public DateOnly To { get; }

    /// <summary>期間の日数（両端を含む）。</summary>
    public int Days => To.DayNumber - From.DayNumber + 1;

    /// <summary>SC-04: 開始 ≤ 終了、最大 366 日、タスクの日付と同じ範囲（2000-01-01〜2099-12-31）。</summary>
    public static ScheduleWindow Create(DateOnly from, DateOnly to)
    {
        if (from < TaskItem.MinDate || from > TaskItem.MaxDate)
        {
            throw new DomainException("validation.failed", "表示期間の開始日は 2000年1月1日〜2099年12月31日の範囲で指定してください。", "from");
        }

        if (to < TaskItem.MinDate || to > TaskItem.MaxDate)
        {
            throw new DomainException("validation.failed", "表示期間の終了日は 2000年1月1日〜2099年12月31日の範囲で指定してください。", "to");
        }

        if (to < from)
        {
            throw new DomainException("validation.failed", "表示期間の終了日は開始日以降の日付にしてください。", "to");
        }

        if (to.DayNumber - from.DayNumber + 1 > MaxDays)
        {
            throw new DomainException("validation.failed", $"表示期間は{MaxDays}日以内で指定してください。", "to");
        }

        return new ScheduleWindow(from, to);
    }

    /// <summary>
    /// SC-01: 棒の範囲。両方あれば開始日〜期限、片方だけならその日の 1 日、どちらもなければ null（日付未設定）。
    /// </summary>
    public static (DateOnly Start, DateOnly End)? BarOf(DateOnly? startDate, DateOnly? dueDate) =>
        (startDate ?? dueDate, dueDate ?? startDate) is ({ } start, { } end) ? (start, end) : null;

    /// <summary>SC-02: 棒が期間と重なるか（境界の日を含む）。日付未設定（SC-03）は重ならない。</summary>
    public bool Overlaps(DateOnly? startDate, DateOnly? dueDate) =>
        BarOf(startDate, dueDate) is { } bar && bar.Start <= To && bar.End >= From;
}
