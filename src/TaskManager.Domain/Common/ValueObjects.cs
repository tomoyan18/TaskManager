using System.Globalization;
using System.Text.RegularExpressions;

namespace TaskManager.Domain.Common;

/// <summary>正規化済みのメールアドレス（domain-model.md §5）。</summary>
public readonly record struct EmailAddress
{
    public const int MaxLength = 254;

    private EmailAddress(string value) => Value = value;

    public string Value { get; }

    /// <summary>前後空白を除去し小文字化する。形式が不正なら DomainException。</summary>
    public static EmailAddress Create(string? input, string field = "email")
    {
        if (!TryCreate(input, out var email))
        {
            throw new DomainException("validation.failed", "メールアドレスの形式が正しくありません。", field);
        }

        return email;
    }

    public static bool TryCreate(string? input, out EmailAddress email)
    {
        email = default;
        var value = Normalize(input);
        if (value.Length is < 3 or > MaxLength)
        {
            return false;
        }

        var at = value.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || at != value.LastIndexOf('@') || at == value.Length - 1)
        {
            return false;
        }

        var domain = value[(at + 1)..];
        if (!domain.Contains('.', StringComparison.Ordinal) || domain.StartsWith('.') || domain.EndsWith('.'))
        {
            return false;
        }

        if (value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            return false;
        }

        email = new EmailAddress(value);
        return true;
    }

    /// <summary>照合用の正規化（前後空白除去・小文字化）。</summary>
    public static string Normalize(string? input) =>
#pragma warning disable CA1308 // メールアドレスは小文字に正規化して保存する仕様（database-design.md §3.1）
        (input ?? string.Empty).Trim().ToLowerInvariant();
#pragma warning restore CA1308

    public override string ToString() => Value;
}

/// <summary>プロジェクトキー（英大文字で始まる 2〜10 文字の英大文字・数字）。作成後は変更できない。</summary>
public readonly partial record struct ProjectKey
{
    public const int MinLength = 2;
    public const int MaxLength = 10;
    public const string Pattern = "^[A-Za-z][A-Za-z0-9]{1,9}$";

    private ProjectKey(string value) => Value = value;

    public string Value { get; }

    public static ProjectKey Create(string? input, string field = "key")
    {
        if (!TryCreate(input, out var key))
        {
            throw new DomainException(
                "validation.failed",
                "プロジェクトキーは英字で始まる 2〜10 文字の英字・数字で入力してください。",
                field);
        }

        return key;
    }

    public static bool TryCreate(string? input, out ProjectKey key)
    {
        key = default;
        var value = (input ?? string.Empty).Trim().ToUpperInvariant();
        if (!KeyRegex().IsMatch(value))
        {
            return false;
        }

        key = new ProjectKey(value);
        return true;
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[A-Z][A-Z0-9]{1,9}$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyRegex();
}

/// <summary>タスクキー（例: SALES-12）。</summary>
public readonly partial record struct TaskKey(ProjectKey ProjectKey, int Number)
{
    public static bool TryParse(string? input, out TaskKey taskKey)
    {
        taskKey = default;
        var match = TaskKeyRegex().Match((input ?? string.Empty).Trim().ToUpperInvariant());
        if (!match.Success
            || !ProjectKey.TryCreate(match.Groups[1].Value, out var projectKey)
            || !int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return false;
        }

        taskKey = new TaskKey(projectKey, number);
        return true;
    }

    public static string Format(string projectKey, int number) =>
        string.Create(CultureInfo.InvariantCulture, $"{projectKey}-{number}");

    public override string ToString() => Format(ProjectKey.Value, Number);

    [GeneratedRegex("^([A-Z][A-Z0-9]{1,9})-([1-9][0-9]{0,8})$", RegexOptions.CultureInvariant)]
    private static partial Regex TaskKeyRegex();
}
