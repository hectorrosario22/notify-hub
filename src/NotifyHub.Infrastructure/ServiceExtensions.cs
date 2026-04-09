using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotifyHub.Core.Repositories;
using NotifyHub.Infrastructure.Persistence;
using NotifyHub.Infrastructure.Persistence.Repositories;

namespace NotifyHub.Infrastructure;

public static class ServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<NotifyHubDbContext>(options =>
            options
                .UseNpgsql(configuration.GetConnectionString("Default"))
                .UseSnakeCaseNamingConvention());

        services.AddScoped<INotificationRepository, NotificationRepository>();

        return services;
    }
}
