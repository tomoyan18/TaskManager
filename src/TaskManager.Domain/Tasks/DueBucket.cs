namespace TaskManager.Domain.Tasks;

/// <summary>「自分のタスク」の期限区分（requirements.md REQ-MY-01）。</summary>
public enum DueBucket
{
    Overdue,
    Today,
    ThisWeek,
    Later,
    NoDueDate,
}

public static class DueBucketClassifier
{
    /// <summary>
    /// 期限を区分する。週は月曜始まり。今週 = 明日〜今週の日曜日（今日が日曜日なら該当なし）。
    /// </summary>
    public static DueBucket Classify(DateOnly? dueDate, DateOnly today)
    {
        if (dueDate is not { } due)
        {
            return DueBucket.NoDueDate;
        }

        if (due < today)
        {
            return DueBucket.Overdue;
        }

        if (due == today)
        {
            return DueBucket.Today;
        }

        return due <= EndOfWeek(today) ? DueBucket.ThisWeek : DueBucket.Later;
    }

    /// <summary>今週の日曜日。</summary>
    public static DateOnly EndOfWeek(DateOnly today)
    {
        var daysUntilSunday = ((int)DayOfWeek.Sunday - (int)today.DayOfWeek + 7) % 7;
        return today.AddDays(daysUntilSunday);
    }
}
