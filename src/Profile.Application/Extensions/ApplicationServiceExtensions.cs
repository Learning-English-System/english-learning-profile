using Microsoft.Extensions.DependencyInjection;
using Profile.Application.Services;

namespace Profile.Application.Extensions;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddProfileApplication(this IServiceCollection services)
    {
        services.AddScoped<IProfileService, ProfileService>();
        return services;
    }
}
