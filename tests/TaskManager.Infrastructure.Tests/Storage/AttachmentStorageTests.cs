using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using TaskManager.Application.Abstractions;
using TaskManager.Infrastructure.Security;
using TaskManager.Infrastructure.Storage;

namespace TaskManager.Infrastructure.Tests.Storage;

public sealed class LocalAttachmentStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "tm-storage-test", Guid.NewGuid().ToString("N"));
    private readonly LocalAttachmentStorage _storage;

    public LocalAttachmentStorageTests()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 8, 1, 0, 0, TimeSpan.Zero));
        _storage = new LocalAttachmentStorage(Options.Create(new AttachmentStorageOptions { RootPath = _root }), time);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task Save_年月とIDのキーで保存し読み出し削除できる()
    {
        var id = Guid.CreateVersion7();
        using var content = new MemoryStream("添付の中身"u8.ToArray());

        var key = await _storage.SaveAsync(id, content, Ct);

        Assert.Equal($"2026/10/{id:N}", key);
        await using (var stream = await _storage.OpenReadAsync(key, Ct))
        {
            Assert.NotNull(stream);
            using var reader = new StreamReader(stream);
            Assert.Equal("添付の中身", await reader.ReadToEndAsync(Ct));
        }

        var listed = new List<StoredFile>();
        await foreach (var file in _storage.ListAsync(Ct))
        {
            listed.Add(file);
        }

        Assert.Equal(key, Assert.Single(listed).StorageKey);
        await _storage.DeleteAsync(key, Ct);
        Assert.Null(await _storage.OpenReadAsync(key, Ct));
    }

    [Fact]
    public async Task Save_同じIDで上書きしない()
    {
        var id = Guid.CreateVersion7();
        await _storage.SaveAsync(id, new MemoryStream([1]), Ct);
        await Assert.ThrowsAsync<IOException>(() => _storage.SaveAsync(id, new MemoryStream([2]), Ct));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("2026/../../outside")]
    [InlineData("/etc/passwd")]
    [InlineData("")]
    public async Task ルートの外を指すキーは拒否する(string key)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _storage.OpenReadAsync(key, Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _storage.DeleteAsync(key, Ct));
    }
}

public class AttachmentContentInspectorTests
{
    private readonly AttachmentContentInspector _inspector = new();

    public static TheoryData<string, byte[], string> Allowed() => new()
    {
        { "a.pdf", "%PDF-1.7"u8.ToArray(), "application/pdf" },
        { "a.PNG", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0], "image/png" },
        { "a.jpeg", [0xFF, 0xD8, 0xFF, 0xE0], "image/jpeg" },
        { "a.gif", "GIF89a"u8.ToArray(), "image/gif" },
        { "a.webp", [.. "RIFF"u8, 0, 0, 0, 0, .. "WEBP"u8], "image/webp" },
        { "a.zip", [0x50, 0x4B, 0x03, 0x04], "application/zip" },
        { "a.xlsx", [0x50, 0x4B, 0x03, 0x04], "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
        { "a.csv", "列1,列2\r\n1,2"u8.ToArray(), "text/csv" },
        { "a.md", "# 見出し\n\t本文"u8.ToArray(), "text/markdown" },
    };

    [Theory]
    [MemberData(nameof(Allowed))]
    public void Inspect_許可された形式で中身が一致する(string fileName, byte[] header, string contentType)
    {
        Assert.Equal(new AttachmentInspection(true, contentType), _inspector.Inspect(fileName, header));
    }

    public static TheoryData<string, byte[]> Rejected() => new()
    {
        { "a.pdf", "<html>"u8.ToArray() },
        { "a.png", "%PDF-"u8.ToArray() },
        { "a.webp", "RIFF0000WAVE"u8.ToArray() },
        { "a.docx", "%PDF-"u8.ToArray() },
        { "a.txt", [0x41, 0x00] },
        { "a.svg", "<svg onload=alert(1)>"u8.ToArray() },
        { "a.html", "<html>"u8.ToArray() },
        { "a.exe", "MZ"u8.ToArray() },
        { "a.pdf.exe", "%PDF-"u8.ToArray() },
        { "noext", "%PDF-"u8.ToArray() },
    };

    [Theory]
    [MemberData(nameof(Rejected))]
    public void Inspect_許可外の形式や中身の偽装は拒否する(string fileName, byte[] header)
    {
        Assert.Equal(new AttachmentInspection(false, null), _inspector.Inspect(fileName, header));
    }
}

public class SecurityPrimitiveTests
{
    [Fact]
    public void PasswordHasher_ハッシュは平文を含まず検証できる()
    {
        var hasher = new PasswordHasherAdapter();
        var hash = hasher.Hash("Kanagawa-Minato-2026");
        Assert.DoesNotContain("Kanagawa", hash, StringComparison.Ordinal);
        Assert.Equal(PasswordCheckResult.Success, hasher.Verify(hash, "Kanagawa-Minato-2026"));
        Assert.Equal(PasswordCheckResult.Failed, hasher.Verify(hash, "kanagawa-minato-2026"));
        Assert.Equal(PasswordCheckResult.Failed, hasher.Verify("not-a-hash", "Kanagawa-Minato-2026"));
        Assert.NotEqual(hash, hasher.Hash("Kanagawa-Minato-2026"));
    }

    [Fact]
    public void RefreshToken_十分な長さの乱数でハッシュは決定的()
    {
        var generator = new RefreshTokenGenerator();
        var a = generator.Generate();
        var b = generator.Generate();
        Assert.NotEqual(a.Token, b.Token);
        Assert.True(a.Token.Length >= 43);
        Assert.DoesNotContain('+', a.Token);
        Assert.DoesNotContain('/', a.Token);
        Assert.Equal(32, a.Hash.Length);
        Assert.Equal(a.Hash, generator.Hash(a.Token));
    }

    [Fact]
    public void TemporaryPassword_パスワードポリシーを満たし紛らわしい文字を含まない()
    {
        var generator = new TemporaryPasswordGenerator();
        for (var i = 0; i < 200; i++)
        {
            var password = generator.Generate();
            Assert.Equal(16, password.Length);
            Assert.Null(Application.Security.PasswordPolicy.Validate(password, "user@example.test", "テスト"));
            Assert.DoesNotContain(password, c => c is '0' or 'O' or '1' or 'l' or 'I');
            Assert.Contains(password, char.IsAsciiLetterUpper);
            Assert.Contains(password, char.IsAsciiLetterLower);
            Assert.Contains(password, char.IsAsciiDigit);
        }
    }
}
