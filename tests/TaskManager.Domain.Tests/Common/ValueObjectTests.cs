using TaskManager.Domain.Common;

namespace TaskManager.Domain.Tests.Common;

public class EmailAddressTests
{
    [Theory]
    [InlineData("  Owner@Example.TEST ", "owner@example.test")]
    [InlineData("a@b.jp", "a@b.jp")]
    public void Create_有効な形式なら前後空白を除き小文字に正規化する(string input, string expected)
    {
        Assert.Equal(expected, EmailAddress.Create(input).Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-at-mark.example.test")]
    [InlineData("@example.test")]
    [InlineData("user@")]
    [InlineData("user@localhost")]
    [InlineData("user@.example")]
    [InlineData("user@example.")]
    [InlineData("a@b@example.test")]
    [InlineData("user name@example.test")]
    [InlineData("user\u0001@example.test")]
    public void Create_不正な形式はDomainException(string? input)
    {
        var ex = Assert.Throws<DomainException>(() => EmailAddress.Create(input));
        Assert.Equal("validation.failed", ex.Code);
        Assert.Equal("email", ex.Field);
    }

    [Fact]
    public void Create_254文字は許可し255文字は拒否する()
    {
        var local = new string('a', 64);
        var domainPart = new string('b', 254 - 64 - 1 - 5) + ".test";
        var max = $"{local}@{domainPart}";
        Assert.Equal(254, max.Length);
        Assert.True(EmailAddress.TryCreate(max, out _));
        Assert.False(EmailAddress.TryCreate("c" + max, out _));
    }
}

public class ProjectKeyTests
{
    [Theory]
    [InlineData("sales", "SALES")]
    [InlineData(" Ab ", "AB")]
    [InlineData("A123456789", "A123456789")]
    public void Create_英字で始まる2から10文字を大文字に正規化する(string input, string expected)
    {
        Assert.Equal(expected, ProjectKey.Create(input).Value);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("A1234567890")]
    [InlineData("1ABC")]
    [InlineData("AB-C")]
    [InlineData("ＡＢ")]
    [InlineData("")]
    [InlineData(null)]
    public void Create_規則外はDomainException(string? input)
    {
        var ex = Assert.Throws<DomainException>(() => ProjectKey.Create(input));
        Assert.Equal("key", ex.Field);
    }
}

public class TaskKeyTests
{
    [Theory]
    [InlineData("SALES-12", "SALES", 12)]
    [InlineData(" sales-1 ", "SALES", 1)]
    [InlineData("A1-999999999", "A1", 999999999)]
    public void TryParse_キーを解析する(string input, string projectKey, int number)
    {
        Assert.True(TaskKey.TryParse(input, out var key));
        Assert.Equal(projectKey, key.ProjectKey.Value);
        Assert.Equal(number, key.Number);
        Assert.Equal($"{projectKey}-{number}", key.ToString());
    }

    [Theory]
    [InlineData("SALES-0")]
    [InlineData("SALES-01")]
    [InlineData("SALES-1234567890")]
    [InlineData("SALES12")]
    [InlineData("-12")]
    [InlineData("1A-2")]
    [InlineData("SALES-1 OR 1=1")]
    [InlineData(null)]
    public void TryParse_不正な形式はfalse(string? input)
    {
        Assert.False(TaskKey.TryParse(input, out _));
    }
}

public class TextRulesTests
{
    [Fact]
    public void RequireSingleLine_前後空白を除く()
    {
        Assert.Equal("見積書", TextRules.RequireSingleLine("  見積書\t", 10, "c", "f", "項目"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void RequireSingleLine_空は入力を求める(string? value)
    {
        var ex = Assert.Throws<DomainException>(() => TextRules.RequireSingleLine(value, 10, "c", "title", "タイトル"));
        Assert.Equal("タイトルを入力してください。", ex.Message);
        Assert.Equal("title", ex.Field);
    }

    [Fact]
    public void RequireSingleLine_最大長ちょうどは許可し超過は拒否する()
    {
        Assert.Equal(10, TextRules.RequireSingleLine(new string('あ', 10), 10, "c", "f", "項目").Length);
        var ex = Assert.Throws<DomainException>(() => TextRules.RequireSingleLine(new string('あ', 11), 10, "c", "f", "項目"));
        Assert.Equal("項目は10文字以内で入力してください。", ex.Message);
    }

    [Theory]
    [InlineData("一行目\n二行目")]
    [InlineData("制御\u0007文字")]
    public void RequireSingleLine_改行や制御文字を拒否する(string value)
    {
        Assert.Throws<DomainException>(() => TextRules.RequireSingleLine(value, 100, "c", "f", "項目"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \n ")]
    public void OptionalMultiline_空はnull(string? value)
    {
        Assert.Null(TextRules.OptionalMultiline(value, 10, "c", "f", "説明"));
    }

    [Fact]
    public void OptionalMultiline_改行とタブは許可しその他の制御文字は拒否する()
    {
        Assert.Equal("a\r\nb\tc", TextRules.OptionalMultiline(" a\r\nb\tc ", 100, "c", "f", "説明"));
        Assert.Throws<DomainException>(() => TextRules.OptionalMultiline("a\u0000b", 100, "c", "f", "説明"));
        Assert.Throws<DomainException>(() => TextRules.OptionalMultiline(new string('a', 101), 100, "c", "f", "説明"));
    }
}
