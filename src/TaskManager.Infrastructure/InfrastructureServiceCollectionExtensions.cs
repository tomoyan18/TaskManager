using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskManager.Application.Abstractions;
using TaskManager.Infrastructure.Persistence;
using TaskManager.Infrastructure.Security;
using TaskManager.Infrastructure.Storage;

namespace TaskManager.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>接続文字列の設定キー（環境変数では ConnectionStrings__TaskManager）。</summary>
    public const string ConnectionStringName = "TaskManager";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddScoped<AuditingSaveChangesInterceptor>();
        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var connectionString = configuration.GetConnectionString(ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"接続文字列 '{ConnectionStringName}' が設定されていません。環境変数 ConnectionStrings__{ConnectionStringName} を設定してください。");
            }

            options.UseMySQL(connectionString);
            options.AddInterceptors(serviceProvider.GetRequiredService<AuditingSaveChangesInterceptor>());
        });
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<ITaskNumberAllocator, MySqlTaskNumberAllocator>();

        services.AddSingleton<IPasswordHasher, PasswordHasherAdapter>();
        services.AddSingleton<IRefreshTokenGenerator, RefreshTokenGenerator>();
        services.AddSingleton<ITemporaryPasswordGenerator, TemporaryPasswordGenerator>();

        services.AddOptions<AttachmentStorageOptions>().Bind(configuration.GetSection(AttachmentStorageOptions.SectionName));
        services.AddSingleton<IAttachmentStorage, LocalAttachmentStorage>();
        services.AddSingleton<IAttachmentContentInspector, AttachmentContentInspector>();
        return services;
    }
}
