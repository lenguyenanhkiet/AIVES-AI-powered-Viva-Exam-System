using AIpoweredVivaExamSystem.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace AIpoweredVivaExamSystem.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Use ASP.NET Core Identity's built-in password hasher (manages salt automatically)
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        return services;
    }
}

