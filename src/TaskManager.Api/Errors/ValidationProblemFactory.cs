using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TaskManager.Application.Common;

namespace TaskManager.Api.Errors;

/// <summary>
/// モデル検証（DataAnnotations）・JSON の形式エラーを Problem Details に変換する。
/// 英語のフレームワーク既定メッセージは日本語に置き換える（CLAUDE.md §15）。
/// </summary>
public static class ValidationProblemFactory
{
    public static IActionResult Create(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // フォームの読み込み中に要求の上限を超えた場合は、形式エラーではなく 413 にする（添付の上限。Phase 6 で検出）
        if (context.ModelState.Values.SelectMany(e => e.Errors).Any(IsTooLarge))
        {
            return ProblemResults.Create(context.HttpContext, StatusCodes.Status413PayloadTooLarge, ErrorCodes.AttachmentTooLarge,
                "ファイルサイズが上限（10MB）を超えています。");
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var malformed = false;
        foreach (var (key, entry) in context.ModelState)
        {
            if (entry.Errors.Count == 0)
            {
                continue;
            }

            var field = NormalizeKey(key);
            if (field.Length == 0 || field is "request" or "body")
            {
                malformed = true;
                continue;
            }

            // 同じ項目に複数の規則が当てはまる場合（空欄で「必須」と「文字数」など）は最初の 1 件だけを示す
            errors[field] = [Localize(entry.Errors[0])];
        }

        return malformed && errors.Count == 0
            ? ProblemResults.Create(context.HttpContext, StatusCodes.Status400BadRequest, ErrorCodes.RequestMalformed,
                "リクエストの形式が正しくありません。")
            : ProblemResults.Create(context.HttpContext, StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed,
                "入力内容に誤りがあります。", "赤く表示された項目を確認してください。", errors);
    }

    /// <summary>"$.dueDate" や "Title" を camelCase のプロパティ名に揃える。</summary>
    private static string NormalizeKey(string key)
    {
        var k = key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key == "$" ? string.Empty : key;
        var bracket = k.IndexOf('[', StringComparison.Ordinal);
        if (bracket > 0)
        {
            k = k[..bracket];
        }

        return k.Length == 0 ? k : char.ToLowerInvariant(k[0]) + k[1..];
    }

    private static string Localize(ModelError error)
    {
        var message = error.ErrorMessage;
        return error.Exception is not null || string.IsNullOrEmpty(message) || IsAscii(message)
            ? "入力の形式が正しくありません。"
            : message;
    }

    /// <summary>
    /// 要求の上限超過か。フォームの読み込み失敗（ValueProviderException）は例外ではなくメッセージだけが残るため、両方を確認する。
    /// </summary>
    private static bool IsTooLarge(ModelError error)
    {
        static bool Matches(string? message) =>
            message is not null
            && (message.Contains("body length limit", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Request body too large", StringComparison.OrdinalIgnoreCase));

        if (Matches(error.ErrorMessage))
        {
            return true;
        }

        for (var e = error.Exception; e is not null; e = e.InnerException)
        {
            if (e is BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } || Matches(e.Message))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAscii(string value) => Encoding.UTF8.GetByteCount(value) == value.Length;
}
