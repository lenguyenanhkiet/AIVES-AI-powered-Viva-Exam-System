using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIpoweredVivaExamSystem.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Local đọc appsettings.Development.json; Docker ghi đè bằng biến môi trường.
        // DbContext chỉ nhận options qua DI, không tự đọc file hoặc lưu credentials.
        var connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' was not found.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<AIpoweredVivaExamSystem.Application.Authentication.ILoginUserRepository,
            AIpoweredVivaExamSystem.Persistence.Repositories.LoginUserRepository>();
        return services;
    }
}
