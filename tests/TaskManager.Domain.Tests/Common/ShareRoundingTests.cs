using TaskManager.Domain.Common;

namespace TaskManager.Domain.Tests.Common;

/// <summary>ステータス別の割合の丸め（domain-model.md §5.2 SR-01〜SR-04）。</summary>
public class ShareRoundingTests
{
    [Fact]
    public void 割り切れる場合はそのままの割合()
    {
        Assert.Equal([50.0m, 25.0m, 25.0m], ShareRounding.Percentages([2, 1, 1]));
    }

    [Fact]
    public void 三等分は端数を先の項目に配り合計を100にする()
    {
        var result = ShareRounding.Percentages([1, 1, 1]);

        Assert.Equal([33.4m, 33.3m, 33.3m], result);
        Assert.Equal(100.0m, result.Sum());
    }

    [Fact]
    public void 端数の大きい項目から配る()
    {
        // 5/12 = 41.666…、4/12 = 33.333…、3/12 = 25.0 → 41.6 + 33.3 + 25.0 = 99.9 → 端数の大きい 41.6 を 41.7 にする
        var result = ShareRounding.Percentages([5, 4, 3]);

        Assert.Equal([41.7m, 33.3m, 25.0m], result);
        Assert.Equal(100.0m, result.Sum());
    }

    [Fact]
    public void 件数0の項目は0のまま残す()
    {
        var result = ShareRounding.Percentages([0, 7, 0, 0]);

        Assert.Equal([0m, 100.0m, 0m, 0m], result);
    }

    [Fact]
    public void 合計0件なら全て0()
    {
        Assert.Equal([0m, 0m, 0m], ShareRounding.Percentages([0, 0, 0]));
        Assert.Empty(ShareRounding.Percentages([]));
    }

    [Theory]
    [InlineData(new[] { 1, 2, 3, 4, 5, 6, 7 })]
    [InlineData(new[] { 999, 1, 1 })]
    [InlineData(new[] { 1, 1, 1, 1, 1, 1 })]
    [InlineData(new[] { 10000, 3, 7, 0, 1 })]
    public void 小数第1位で合計が常に100になる(int[] counts)
    {
        var result = ShareRounding.Percentages(counts);

        Assert.Equal(100.0m, result.Sum());
        Assert.All(result, p => Assert.Equal(p, Math.Round(p, 1)));
    }

    [Fact]
    public void 負の件数は受け付けない()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ShareRounding.Percentages([1, -1]));
    }
}
