using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Infrastructure.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
namespace AIpoweredVivaExamSystem.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Đăng ký và Login phải dùng cùng định dạng hash.
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IPasswordVerifier, IdentityPasswordVerifier>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        return services;
    }
}
