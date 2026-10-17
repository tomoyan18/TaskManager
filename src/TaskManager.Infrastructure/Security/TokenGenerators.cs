using System.Security.Cryptography;
using System.Text;
using TaskManager.Application.Abstractions;

namespace TaskManager.Infrastructure.Security;

/// <summary>リフレッシュトークン（ランダム 32 バイト、DB には SHA-256 のみ保存）。</summary>
public sealed class RefreshTokenGenerator : IRefreshTokenGenerator
{
    public GeneratedRefreshToken Generate()
    {
        var token = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        return new GeneratedRefreshToken(token, Hash(token));
    }

    public byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>一時パスワード（16 文字、紛らわしい文字 0 O 1 l I を除く。authentication-authorization.md §5）。</summary>
public sealed class TemporaryPasswordGenerator : ITemporaryPasswordGenerator
{
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!#$%&*+-=?@";
    private const int Length = 16;

    public string Generate()
    {
        var all = Upper + Lower + Digits + Symbols;
        var chars = new char[Length];
        chars[0] = Pick(Upper);
        chars[1] = Pick(Lower);
        chars[2] = Pick(Digits);
        chars[3] = Pick(Symbols);
        for (var i = 4; i < Length; i++)
        {
            chars[i] = Pick(all);
        }

        RandomNumberGenerator.Shuffle(chars.AsSpan());
        var result = new string(chars);
        return HasRunOfFour(result) ? Generate() : result;
    }

    private static char Pick(string source) => source[RandomNumberGenerator.GetInt32(source.Length)];

    private static bool HasRunOfFour(string s)
    {
        for (var i = 3; i < s.Length; i++)
        {
            if (s[i] == s[i - 1] && s[i] == s[i - 2] && s[i] == s[i - 3])
            {
                return true;
            }
        }

        return false;
    }
}
