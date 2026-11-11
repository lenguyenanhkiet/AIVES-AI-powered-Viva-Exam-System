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
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddScoped<AIpoweredVivaExamSystem.Application.QuestionBank.IQuestionFileReader, QuestionBank.QuestionFileReader>();
        services.AddScoped<AIpoweredVivaExamSystem.Application.QuestionBank.IDocumentTextExtractor, QuestionBank.DocumentTextExtractor>();
        services.AddHttpClient<AIpoweredVivaExamSystem.Application.QuestionBank.IQuestionGenerator, QuestionBank.OpenAiQuestionGenerator>(client => client.Timeout = TimeSpan.FromSeconds(90));
        services.AddSingleton<AIpoweredVivaExamSystem.Application.VivaSessions.IAnswerAudioStorage>(_ => new VivaSessions.LocalAnswerAudioStorage());
        services.AddHttpClient<AIpoweredVivaExamSystem.Application.VivaSessions.IVivaAiExaminer, VivaSessions.OpenAiVivaExaminer>(client => client.Timeout = TimeSpan.FromSeconds(25));
        services.AddHttpClient<AIpoweredVivaExamSystem.Application.VivaSessions.ISpeechTranscriber, VivaSessions.OpenAiSpeechTranscriber>(client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddHttpClient<AIpoweredVivaExamSystem.Application.VivaSessions.IVivaAiGrader, VivaSessions.OpenAiVivaGrader>(client => client.Timeout = TimeSpan.FromSeconds(45));
        return services;
    }
}
