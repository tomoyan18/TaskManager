using Microsoft.AspNetCore.Identity;
using TaskManager.Application.Abstractions;
using TaskManager.Domain.Accounts;

namespace TaskManager.Infrastructure.Security;

/// <summary>PBKDF2（HMAC-SHA512）によるパスワードハッシュ（security-design.md S-05）。</summary>
public sealed class PasswordHasherAdapter : Application.Abstractions.IPasswordHasher
{
    private static readonly UserAccount HashContext = null!;
    private readonly PasswordHasher<UserAccount> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(HashContext, password);

    public PasswordCheckResult Verify(string passwordHash, string password)
    {
        try
        {
            return _hasher.VerifyHashedPassword(HashContext, passwordHash, password) switch
            {
                PasswordVerificationResult.Success => PasswordCheckResult.Success,
                PasswordVerificationResult.SuccessRehashNeeded => PasswordCheckResult.SuccessRehashNeeded,
                _ => PasswordCheckResult.Failed,
            };
        }
        catch (FormatException)
        {
            return PasswordCheckResult.Failed;
        }
    }
}
