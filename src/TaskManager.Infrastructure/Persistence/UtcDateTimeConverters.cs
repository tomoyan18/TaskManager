using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TaskManager.Infrastructure.Persistence;

/// <summary>DB への保存時は UTC に変換し、読み込み時は Kind=Utc を付ける。</summary>
internal sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

internal sealed class NullableUtcDateTimeConverter() : ValueConverter<DateTime?, DateTime?>(
    v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v.Value : v.Value.ToUniversalTime()) : v,
    v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

/// <summary>DateOnly ⇔ DateTime（MySQL の DATE 列用）。</summary>
internal sealed class DateOnlyConverter() : ValueConverter<DateOnly, DateTime>(
    v => v.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
    v => DateOnly.FromDateTime(v));

internal sealed class NullableDateOnlyConverter() : ValueConverter<DateOnly?, DateTime?>(
    v => v.HasValue ? v.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified) : null,
    v => v.HasValue ? DateOnly.FromDateTime(v.Value) : null);
