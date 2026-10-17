using Microsoft.Extensions.DependencyInjection;
using TaskManager.Application.Auth;
using TaskManager.Application.Maintenance;
using TaskManager.Application.Notices;
using TaskManager.Application.Projects;
using TaskManager.Application.Security;
using TaskManager.Application.Tasks;
using TaskManager.Application.Users;

namespace TaskManager.Application;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>Application 層のサービスを登録する（1 リクエスト = 1 スコープ）。</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<ProjectAccessGuard>();
        services.AddScoped<SecurityAuditService>();
        services.AddScoped<NoticeWriter>();
        services.AddScoped<TaskProjection>();

        services.AddScoped<AuthService>();
        services.AddScoped<MyAccountService>();
        services.AddScoped<UserAdministrationService>();
        services.AddScoped<UserDirectoryService>();
        services.AddScoped<BootstrapAdministratorService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<ProjectMembershipService>();
        services.AddScoped<WorkflowStatusService>();
        services.AddScoped<TagService>();
        services.AddScoped<MilestoneService>();
        services.AddScoped<TaskItemService>();
        services.AddScoped<TaskHistoryService>();
        services.AddScoped<TaskCommentService>();
        services.AddScoped<TaskAttachmentService>();
        services.AddScoped<MyTasksService>();
        services.AddScoped<TaskScheduleService>();
        services.AddScoped<TaskStatusSummaryService>();
        services.AddScoped<TaskSearchService>();
        services.AddScoped<NoticeService>();
        services.AddScoped<MaintenanceService>();
        services.TryAddTimeProvider();
        return services;
    }

    private static void TryAddTimeProvider(this IServiceCollection services)
    {
        if (!services.Any(d => d.ServiceType == typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }
    }
}
