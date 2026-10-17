namespace TaskManager.Application.Common;

public static class TimeProviderExtensions
{
    /// <summary>現在の UTC 日時（DateTime, Kind=Utc）。</summary>
    public static DateTime UtcNow(this TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        return timeProvider.GetUtcNow().UtcDateTime;
    }

    /// <summary>利用者のタイムゾーンでの今日の日付。</summary>
    public static DateOnly TodayIn(this TimeProvider timeProvider, string timeZoneId)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        var zone = TimeZones.FindOrDefault(timeZoneId);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), zone).DateTime);
    }
}

public static class TimeZones
{
    public const string Default = "Asia/Tokyo";

    public static bool IsValid(string? timeZoneId) =>
        !string.IsNullOrWhiteSpace(timeZoneId) && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _);

    public static TimeZoneInfo FindOrDefault(string? timeZoneId) =>
        timeZoneId is not null && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var zone)
            ? zone
            : TimeZoneInfo.TryFindSystemTimeZoneById(Default, out var tokyo) ? tokyo : TimeZoneInfo.Utc;
}
