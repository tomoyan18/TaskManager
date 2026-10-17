using TaskManager.Application.Common;

namespace TaskManager.Application.Security;

/// <summary>パスワードポリシー（authentication-authorization.md §5）。</summary>
public static class PasswordPolicy
{
    public const int MinLength = 12;
    public const int MaxLength = 128;

    // TaskManager 用に独自に用意した、推測されやすいパスワードの一覧（小文字で比較する）
    private static readonly HashSet<string> WeakPasswords = new(StringComparer.Ordinal)
    {
        "password1234", "password12345", "password123456", "passw0rd1234", "p@ssw0rd1234",
        "taskmanager", "taskmanager1", "taskmanager123", "taskmanager2026",
        "qwertyuiop", "qwertyuiop12", "qwerty123456", "asdfghjkl123", "zxcvbnm12345",
        "123456789012", "1234567890ab", "abcdefghijkl", "abc123456789", "111111111111", "000000000000",
        "iloveyou1234", "welcome12345", "letmein12345", "admin1234567", "administrator", "changeme1234",
    };

    /// <summary>違反があればメッセージを返す（なければ null）。</summary>
    public static string? Validate(string password, string? email, string? displayName)
    {
        ArgumentNullException.ThrowIfNull(password);
        if (password.Length < MinLength)
        {
            return $"パスワードは{MinLength}文字以上で設定してください。";
        }

        if (password.Length > MaxLength)
        {
            return $"パスワードは{MaxLength}文字以内で設定してください。";
        }

        if (password != password.Trim())
        {
            return "パスワードの先頭と末尾に空白は使えません。";
        }

#pragma warning disable CA1308 // 弱いパスワード一覧との比較用
        var lower = password.ToLowerInvariant();
#pragma warning restore CA1308
        if (WeakPasswords.Contains(lower))
        {
            return "よく使われるパスワードのため使えません。別のパスワードを設定してください。";
        }

        var localPart = email?.Split('@')[0];
        if (!string.IsNullOrEmpty(localPart) && localPart.Length >= 3
            && lower.Contains(localPart, StringComparison.OrdinalIgnoreCase))
        {
            return "メールアドレスを含むパスワードは使えません。";
        }

        if (!string.IsNullOrWhiteSpace(displayName) && displayName.Trim().Length >= 3
            && password.Contains(displayName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return "表示名を含むパスワードは使えません。";
        }

        for (var i = 3; i < password.Length; i++)
        {
            if (password[i] == password[i - 1] && password[i] == password[i - 2] && password[i] == password[i - 3])
            {
                return "同じ文字を 4 回以上続けて使うことはできません。";
            }
        }

        return null;
    }

    public static void Ensure(string password, string? email, string? displayName, string field)
    {
        var error = Validate(password, email, displayName);
        if (error is not null)
        {
            throw Errors.Validation(field, error);
        }
    }
}
