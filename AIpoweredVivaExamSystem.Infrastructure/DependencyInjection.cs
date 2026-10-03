using AIpoweredVivaExamSystem.Application.Common.Interfaces;
using AIpoweredVivaExamSystem.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AIpoweredVivaExamSystem.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddTransient<IPasswordHasher, PasswordHasher>();
        return services;
    }
}

