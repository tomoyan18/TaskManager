using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace TaskManager.Application.Common;

/// <summary>
/// PATCH 用の Request の基底。JSON に含まれていたプロパティだけを「指定あり」として記録し、
/// 省略（変更しない）と null（値を消す）を区別する（docs/design/api-design.md §1「部分更新」）。
/// </summary>
public abstract class PatchRequest
{
    private readonly HashSet<string> _specified = new(StringComparer.Ordinal);

    /// <summary>プロパティが JSON に含まれていたか。</summary>
    public bool IsSpecified(string propertyName) => _specified.Contains(propertyName);

    [JsonIgnore]
    public IReadOnlyCollection<string> SpecifiedProperties => _specified;

    protected T Set<T>(T value, [CallerMemberName] string propertyName = "")
    {
        _specified.Add(propertyName);
        return value;
    }
}
