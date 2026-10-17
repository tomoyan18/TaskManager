using System.Globalization;
using TaskManager.Application.Abstractions;
using TaskManager.Domain.Accounts;

namespace TaskManager.Testing;

/// <summary>テスト用の現在の利用者（1 スコープ = 1 リクエスト）。</summary>
public sealed class TestCurrentUser : ICurrentUser
{
    private Guid? _userId;

    public bool IsAuthenticated => _userId is not null;

    public Guid UserId => _userId ?? throw new InvalidOperationException("ログインしていません。");

    public bool IsSystemAdministrator { get; private set; }

    public Guid? SessionFamilyId { get; private set; }

    public string? IpAddress => "192.0.2.10";

    public string? UserAgent => "TaskManager.Tests";

    public void SignIn(TestUser? user)
    {
        _userId = user?.Id;
        IsSystemAdministrator = user?.IsSystemAdministrator ?? false;
        SessionFamilyId = user is null ? null : Guid.NewGuid();
    }
}

/// <summary>Application テスト用のアクセストークン発行（中身は識別できる文字列のみ）。</summary>
public sealed class FakeAccessTokenIssuer : IAccessTokenIssuer
{
    public AccessToken Issue(UserAccount user, Guid sessionFamilyId)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new AccessToken(string.Create(CultureInfo.InvariantCulture, $"test-token:{user.Id}:{sessionFamilyId}"), 900);
    }
}

/// <summary>テストデータの利用者。Password は平文（テスト内でのログインに使う架空の値）。</summary>
public sealed record TestUser(Guid Id, string Email, string DisplayName, string Password, bool IsSystemAdministrator);

/// <summary>テストデータのプロジェクト。</summary>
public sealed record TestProject(Guid Id, string Key, string Name);

/// <summary>一意な値の生成（並行するテスト同士でデータが衝突しないようにする）。</summary>
public static class Unique
{
    private static int _counter;

    /// <summary>英大文字で始まる 10 文字のプロジェクトキー。</summary>
    public static string ProjectKey()
    {
        var n = Interlocked.Increment(ref _counter);
        var random = Convert.ToHexString(Guid.NewGuid().ToByteArray())[..5];
        return string.Create(CultureInfo.InvariantCulture, $"T{n % 10000:D4}{random}");
    }

    public static string Email(string prefix = "user") =>
        string.Create(CultureInfo.InvariantCulture, $"{prefix}-{Guid.NewGuid():N}@example.test");

    public static string Name(string prefix) =>
        string.Create(CultureInfo.InvariantCulture, $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}");
}
