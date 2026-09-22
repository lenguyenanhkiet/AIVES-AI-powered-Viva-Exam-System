using AIpoweredVivaExamSystem.Application.Authentication;
using Microsoft.Extensions.DependencyInjection;
namespace AIpoweredVivaExamSystem.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<LoginService>();
        return services;
    }
}
