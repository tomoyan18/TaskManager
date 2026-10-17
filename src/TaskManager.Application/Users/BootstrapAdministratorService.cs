using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Common;
using TaskManager.Application.Security;
using TaskManager.Domain.Accounts;
using TaskManager.Domain.Auditing;
using TaskManager.Domain.Common;

namespace TaskManager.Application.Users;

/// <summary>
/// 初期システム管理者の作成（REQ-USR-09、migration-strategy.md §6）。
/// 有効なシステム管理者が 0 人の場合のみ作成する。値はログに出さない。
/// </summary>
public sealed partial class BootstrapAdministratorService(
    IAppDbContext db, IPasswordHasher passwordHasher, TimeProvider timeProvider, ILogger<BootstrapAdministratorService> logger)
{
    public async Task EnsureAsync(string? email, string? password, CancellationToken cancellationToken)
    {
        if (await db.UserAccounts.AnyAsync(u => u.IsActive && u.SystemRole == SystemRole.Administrator, cancellationToken))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            LogNotConfigured(logger);
            return;
        }

        if (!EmailAddress.TryCreate(email, out var address))
        {
            LogInvalid(logger, "メールアドレスの形式が正しくありません。");
            return;
        }

        var policyError = PasswordPolicy.Validate(password, address.Value, "システム管理者");
        if (policyError is not null)
        {
            LogInvalid(logger, policyError);
            return;
        }

        var existing = await db.UserAccounts.SingleOrDefaultAsync(u => u.Email == address.Value, cancellationToken);
        if (existing is not null)
        {
            LogInvalid(logger, "同じメールアドレスの利用者が既にいます。システム管理者に変更してください。");
            return;
        }

        var admin = UserAccount.Create(address, "システム管理者", SystemRole.Administrator, passwordHasher.Hash(password));
        db.UserAccounts.Add(admin);
        db.SecurityAuditEvents.Add(SecurityAuditEvent.Create(
            SecurityEventKind.UserCreated, null, admin.Id, null, null, null, "{\"source\":\"bootstrap\"}", timeProvider.UtcNow()));
        await db.SaveChangesAsync(cancellationToken);
        LogCreated(logger);
    }

    [LoggerMessage(EventId = 9200, Level = LogLevel.Warning,
        Message = "システム管理者がいません。TM_BOOTSTRAP_ADMIN_EMAIL と TM_BOOTSTRAP_ADMIN_PASSWORD を設定して再起動してください。")]
    private static partial void LogNotConfigured(ILogger logger);

    [LoggerMessage(EventId = 9201, Level = LogLevel.Error, Message = "初期システム管理者を作成できませんでした: {Reason}")]
    private static partial void LogInvalid(ILogger logger, string reason);

    [LoggerMessage(EventId = 9202, Level = LogLevel.Information,
        Message = "初期システム管理者を作成しました。初回ログイン時にパスワードの変更が必要です。")]
    private static partial void LogCreated(ILogger logger);
}
