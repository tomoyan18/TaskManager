using System.ComponentModel.DataAnnotations;

namespace TaskManager.Api.Configuration;

/// <summary>
/// アクセストークン（JWT）の設定（docs/design/authentication-authorization.md §2.1）。
/// SigningKey は Secret のため appsettings に書かず、環境変数 Jwt__SigningKey で渡す。
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>署名鍵の最小バイト数（HS256）。</summary>
    public const int MinimumSigningKeyBytes = 32;

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>Base64 でエンコードした 32 バイト以上のランダム値。</summary>
    [Required]
    public string SigningKey { get; set; } = string.Empty;

    public byte[] GetSigningKeyBytes()
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(SigningKey);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Jwt:SigningKey は Base64 形式で指定してください。", ex);
        }

        if (bytes.Length < MinimumSigningKeyBytes)
        {
            throw new InvalidOperationException(
                $"Jwt:SigningKey は {MinimumSigningKeyBytes} バイト以上にしてください（現在 {bytes.Length} バイト）。");
        }

        return bytes;
    }
}
