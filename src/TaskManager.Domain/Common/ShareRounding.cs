namespace TaskManager.Domain.Common;

/// <summary>
/// 件数から割合（%）を求める。domain-model.md §5.2（SR-01〜SR-04）。
/// 小数第 1 位で表し、最大剰余法で合計を 100.0 にそろえる（合計 0 件なら全て 0）。
/// </summary>
public static class ShareRounding
{
    /// <summary>割合の最小単位（0.1%）を整数で扱うための倍率。</summary>
    private const int UnitsPerHundred = 1000;

    public static IReadOnlyList<decimal> Percentages(IReadOnlyList<int> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        if (counts.Any(c => c < 0))
        {
            throw new ArgumentOutOfRangeException(nameof(counts), "件数に負の値は指定できません。");
        }

        var total = counts.Sum(c => (long)c);
        if (total == 0)
        {
            return counts.Select(_ => 0m).ToList();
        }

        // 0.1% 単位で切り捨て、端数（剰余）を記録する
        var units = new long[counts.Count];
        var remainders = new long[counts.Count];
        for (var i = 0; i < counts.Count; i++)
        {
            var scaled = counts[i] * (long)UnitsPerHundred;
            units[i] = scaled / total;
            remainders[i] = scaled % total;
        }

        // 足りない分を、端数の大きい項目から 0.1 ずつ配る（同じ端数なら先の項目を優先）
        var missing = UnitsPerHundred - units.Sum();
        foreach (var index in Enumerable.Range(0, counts.Count)
                     .Where(i => remainders[i] > 0)
                     .OrderByDescending(i => remainders[i])
                     .ThenBy(i => i)
                     .Take((int)missing))
        {
            units[index]++;
        }

        return units.Select(u => u / 10m).ToList();
    }
}
