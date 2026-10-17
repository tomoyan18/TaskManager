namespace TaskManager.Domain.Common;

/// <summary>
/// 業務ルール（不変条件）に違反したことを表す例外。
/// <see cref="Code"/> は API のエラーコード（docs/design/api-design.md §1.5）と対応し、
/// <see cref="Field"/> は入力項目に起因する場合のプロパティ名（camelCase）。
/// </summary>
public class DomainException : Exception
{
    public DomainException(string code, string message, string? field = null)
        : base(message)
    {
        Code = code;
        Field = field;
    }

    public DomainException()
        : this("domain.rule_violated", "業務ルールに違反しています。")
    {
    }

    public DomainException(string message)
        : this("domain.rule_violated", message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
        Code = "domain.rule_violated";
    }

    public string Code { get; } = "domain.rule_violated";

    public string? Field { get; }
}
