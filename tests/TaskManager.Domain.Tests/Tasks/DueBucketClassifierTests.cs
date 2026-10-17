using TaskManager.Domain.Tasks;

namespace TaskManager.Domain.Tests.Tasks;

public class DueBucketClassifierTests
{
    // 2026-10-07 は水曜日、2026-10-11 は日曜日、2026-10-12 は月曜日
    private static readonly DateOnly Wednesday = new(2026, 10, 7);

    [Theory]
    [InlineData("2026-10-06", DueBucket.Overdue)]
    [InlineData("2026-10-07", DueBucket.Today)]
    [InlineData("2026-10-08", DueBucket.ThisWeek)]
    [InlineData("2026-10-11", DueBucket.ThisWeek)]
    [InlineData("2026-10-12", DueBucket.Later)]
    public void Classify_週は月曜始まりで今週の日曜日までを今週とする(string due, DueBucket expected)
    {
        Assert.Equal(expected, DueBucketClassifier.Classify(DateOnly.ParseExact(due, "yyyy-MM-dd"), Wednesday));
    }

    [Fact]
    public void Classify_期限なし()
    {
        Assert.Equal(DueBucket.NoDueDate, DueBucketClassifier.Classify(null, Wednesday));
    }

    [Fact]
    public void Classify_今日が日曜日なら今週は該当なしで翌日は以降()
    {
        var sunday = new DateOnly(2026, 10, 11);
        Assert.Equal(DueBucket.Today, DueBucketClassifier.Classify(sunday, sunday));
        Assert.Equal(DueBucket.Later, DueBucketClassifier.Classify(sunday.AddDays(1), sunday));
    }

    [Fact]
    public void Classify_今日が月曜日なら日曜日まで今週()
    {
        var monday = new DateOnly(2026, 10, 12);
        Assert.Equal(DueBucket.ThisWeek, DueBucketClassifier.Classify(new DateOnly(2026, 10, 18), monday));
        Assert.Equal(DueBucket.Later, DueBucketClassifier.Classify(new DateOnly(2026, 10, 19), monday));
    }

    [Fact]
    public void EndOfWeek_年またぎ()
    {
        // 2026-12-31 は木曜日
        Assert.Equal(new DateOnly(2027, 1, 3), DueBucketClassifier.EndOfWeek(new DateOnly(2026, 12, 31)));
    }
}
