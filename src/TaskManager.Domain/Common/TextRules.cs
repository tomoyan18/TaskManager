namespace TaskManager.Domain.Common;

/// <summary>文字列項目の共通ルール（前後空白の除去・制御文字の拒否）。</summary>
public static class TextRules
{
    /// <summary>前後空白を除き、1 行のテキスト（改行・制御文字なし）として検証する。</summary>
    public static string RequireSingleLine(string? value, int maxLength, string code, string field, string label)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw new DomainException(code, $"{label}を入力してください。", field);
        }

        if (trimmed.Length > maxLength)
        {
            throw new DomainException(code, $"{label}は{maxLength}文字以内で入力してください。", field);
        }

        if (trimmed.Any(char.IsControl))
        {
            throw new DomainException(code, $"{label}に改行や制御文字は使えません。", field);
        }

        return trimmed;
    }

    /// <summary>複数行を許す任意のテキスト。空なら null を返す。</summary>
    public static string? OptionalMultiline(string? value, int maxLength, string code, string field, string label)
    {
        if (value is null)
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            throw new DomainException(code, $"{label}は{maxLength}文字以内で入力してください。", field);
        }

        if (trimmed.Any(c => char.IsControl(c) && c is not '\n' and not '\r' and not '\t'))
        {
            throw new DomainException(code, $"{label}に使えない制御文字が含まれています。", field);
        }

        return trimmed;
    }
}
