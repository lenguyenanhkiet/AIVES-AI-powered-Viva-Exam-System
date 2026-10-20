using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AIpoweredVivaExamSystem.Application.Interfaces;
using AIpoweredVivaExamSystem.Persistence.Repositories;
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
        var connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' was not found.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IRubricRepository, RubricRepository>();
        services.AddScoped<ExamFoundationStore>();
        services.AddScoped<AIpoweredVivaExamSystem.Application.Exams.IExamAccessStore>(p => p.GetRequiredService<ExamFoundationStore>());
        services.AddScoped<AIpoweredVivaExamSystem.Application.Exams.IExamFoundationReader>(p => p.GetRequiredService<ExamFoundationStore>());
        services.AddScoped<AIpoweredVivaExamSystem.Application.Exams.IExamUnitOfWork, ExamUnitOfWork>();
        services.AddScoped<AIpoweredVivaExamSystem.Application.QuestionBank.IQuestionBankStore, QuestionBankStore>();
        services.AddScoped<AIpoweredVivaExamSystem.Application.Authentication.IAccessStore, AccessStore>();
        services.AddScoped<AIpoweredVivaExamSystem.Application.Authentication.ILoginUserRepository, LoginUserRepository>();
        services.AddScoped<IQuestionLookup, QuestionLookup>();
        services.AddScoped<AIpoweredVivaExamSystem.Application.Scheduling.IExamScheduleStore, ExamScheduleStore>();
        services.AddScoped<ISubjectRepository, SubjectRepository>();
        services.AddScoped<ITopicRepository, TopicRepository>();
        services.AddScoped<IQuestionRepository, QuestionRepository>();

        return services;
    }
}
