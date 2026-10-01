using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Infrastructure.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace AIpoweredVivaExamSystem.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Đăng ký thuật toán hash dùng chung với Register và verifier cho nghiệp vụ Login.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IPasswordVerifier, IdentityPasswordVerifier>();
        return services;
    }
}
