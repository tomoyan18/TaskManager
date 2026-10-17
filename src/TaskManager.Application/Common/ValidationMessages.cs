namespace TaskManager.Application.Common;

/// <summary>
/// DataAnnotations の日本語メッセージ（{0} は Display の名前）。
/// 同じ制約が OpenAPI のスキーマにも反映される（docs/design/technology.md §2）。
/// </summary>
public static class ValidationMessages
{
    public const string Required = "{0}を入力してください。";
    public const string Select = "{0}を選んでください。";
    public const string MaxLength = "{0}は{1}文字以内で入力してください。";
    public const string LengthRange = "{0}は{2}〜{1}文字で入力してください。";
    public const string Range = "{0}は{1}〜{2}の範囲で指定してください。";
    public const string MaxItems = "{0}は{1}件まで指定できます。";
    public const string Email = "メールアドレスの形式が正しくありません。";
    public const string ProjectKey = "プロジェクトキーは英字で始まる 2〜10 文字の英字・数字で入力してください。";
}
