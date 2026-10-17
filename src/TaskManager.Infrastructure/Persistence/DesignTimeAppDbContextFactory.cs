using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TaskManager.Infrastructure.Persistence;

/// <summary>
/// dotnet ef（マイグレーションの追加・バンドル作成）用のファクトリ。
/// マイグレーションの生成時は DB に接続しないため、接続文字列に Secret を含めない。
/// 実際の適用先はバンドル実行時の --connection で指定する（docs/design/migration-strategy.md）。
/// </summary>
public sealed class DesignTimeAppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string DesignTimeConnectionString = "Server=localhost;Port=3306;Database=taskmanager_db";

    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySQL(DesignTimeConnectionString)
            .Options;
        return new AppDbContext(options);
    }
}
