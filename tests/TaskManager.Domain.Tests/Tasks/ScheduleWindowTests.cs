using TaskManager.Domain.Common;
using TaskManager.Domain.Tasks;

namespace TaskManager.Domain.Tests.Tasks;

/// <summary>ガントチャートの表示期間と棒の規則（domain-model.md §5.1 SC-01〜SC-04）。</summary>
public class ScheduleWindowTests
{
    private static DateOnly D(string value) => DateOnly.ParseExact(value, "yyyy-MM-dd");

    [Fact]
    public void Create_両端を含めて366日までは作れる()
    {
        var window = ScheduleWindow.Create(D("2026-10-01"), D("2027-10-01"));

        Assert.Equal(366, window.Days);
        Assert.Equal(1, ScheduleWindow.Create(D("2026-10-08"), D("2026-10-08")).Days);
    }

    [Fact]
    public void Create_367日以上はエラー()
    {
        var ex = Assert.Throws<DomainException>(() => ScheduleWindow.Create(D("2026-10-01"), D("2027-10-02")));
        Assert.Equal("to", ex.Field);
        Assert.Contains("366日以内", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_終了日が開始日より前はエラー()
    {
        var ex = Assert.Throws<DomainException>(() => ScheduleWindow.Create(D("2026-10-08"), D("2026-10-07")));
        Assert.Equal("to", ex.Field);
    }

    [Theory]
    [InlineData("1999-12-31", "2000-01-10", "from")]
    [InlineData("2099-12-25", "2100-01-01", "to")]
    public void Create_タスクの日付と同じ範囲を超えるとエラー(string from, string to, string field)
    {
        Assert.Equal(field, Assert.Throws<DomainException>(() => ScheduleWindow.Create(D(from), D(to))).Field);
    }

    [Fact]
    public void BarOf_開始日と期限から棒の範囲を決め片方だけなら1日にする()
    {
        Assert.Equal((D("2026-10-01"), D("2026-10-09")), ScheduleWindow.BarOf(D("2026-10-01"), D("2026-10-09")));
        Assert.Equal((D("2026-10-09"), D("2026-10-09")), ScheduleWindow.BarOf(null, D("2026-10-09")));
        Assert.Equal((D("2026-10-01"), D("2026-10-01")), ScheduleWindow.BarOf(D("2026-10-01"), null));
        Assert.Null(ScheduleWindow.BarOf(null, null));
    }

    [Theory]
    [InlineData("2026-10-01", "2026-10-04", false)] // 期間より前に終わる
    [InlineData("2026-10-01", "2026-10-05", true)]  // 期間の初日に終わる
    [InlineData("2026-10-10", "2026-10-12", true)]  // 期間の中
    [InlineData("2026-10-18", "2026-10-30", true)]  // 期間の最終日に始まる
    [InlineData("2026-10-19", "2026-10-30", false)] // 期間より後に始まる
    [InlineData("2026-09-01", "2026-11-30", true)]  // 期間を覆う
    [InlineData(null, "2026-10-05", true)]          // 期限だけ（初日）
    [InlineData("2026-10-18", null, true)]          // 開始日だけ（最終日）
    [InlineData(null, "2026-10-04", false)]
    [InlineData(null, null, false)]                 // 日付未設定は重ならない（SC-03）
    public void Overlaps_境界の日を含めて期間と重なるかを判定する(string? start, string? due, bool expected)
    {
        var window = ScheduleWindow.Create(D("2026-10-05"), D("2026-10-18"));

        Assert.Equal(expected, window.Overlaps(start is null ? null : D(start), due is null ? null : D(due)));
    }
}
