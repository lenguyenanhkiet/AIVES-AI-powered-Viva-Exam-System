using AIpoweredVivaExamSystem.DataAccess.Context;
using AIpoweredVivaExamSystem.DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AIpoweredVivaExamSystem.DataAccess;

public static class ServiceRegistration
{
    public static IServiceCollection AddDataAccess(this IServiceCollection services)
    {
        // Resolve configuration when the scope is opened, after host/test overrides have been applied.
        services.AddDbContext<ApplicationDbContext>((provider, options) => options.UseSqlServer(
            provider.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.")));

        services.AddScoped<ExamUnitOfWork>();
        services.AddScoped<ExamFoundationStore>();
        services.AddScoped<IExamAccessStore>(provider => provider.GetRequiredService<ExamFoundationStore>());
        services.AddScoped<IExamFoundationReader>(provider => provider.GetRequiredService<ExamFoundationStore>());
        services.AddScoped<AccessStore>();
        services.AddScoped<IAccessStore>(provider => provider.GetRequiredService<AccessStore>());
        services.AddScoped<LoginUserRepository>();
        services.AddScoped<ILoginUserRepository>(provider => provider.GetRequiredService<LoginUserRepository>());
        services.AddScoped<SubjectRepository>();
        services.AddScoped<ISubjectRepository>(provider => provider.GetRequiredService<SubjectRepository>());
        services.AddScoped<TopicRepository>();
        services.AddScoped<ITopicRepository>(provider => provider.GetRequiredService<TopicRepository>());
        services.AddScoped<QuestionRepository>();
        services.AddScoped<IQuestionRepository>(provider => provider.GetRequiredService<QuestionRepository>());
        services.AddScoped<RubricRepository>();
        services.AddScoped<IRubricRepository>(provider => provider.GetRequiredService<RubricRepository>());
        services.AddScoped<QuestionLookup>();
        services.AddScoped<IQuestionLookup>(provider => provider.GetRequiredService<QuestionLookup>());
        services.AddScoped<ExamRepository>();
        services.AddScoped<IExamRepository>(provider => provider.GetRequiredService<ExamRepository>());
        services.AddScoped<ExamPoolStore>();
        services.AddScoped<IExamPoolStore>(provider => provider.GetRequiredService<ExamPoolStore>());
        services.AddScoped<ExamRosterStore>();
        services.AddScoped<IExamRosterStore>(provider => provider.GetRequiredService<ExamRosterStore>());
        services.AddScoped<ExamScheduleStore>();
        services.AddScoped<IExamScheduleStore>(provider => provider.GetRequiredService<ExamScheduleStore>());
        services.AddScoped<QuestionBankStore>();
        services.AddScoped<IQuestionBankStore>(provider => provider.GetRequiredService<QuestionBankStore>());
        services.AddScoped<VivaSessionStore>();
        services.AddScoped<IVivaSessionStore>(provider => provider.GetRequiredService<VivaSessionStore>());
        services.AddScoped<VivaInterviewStore>();
        services.AddScoped<IVivaInterviewStore>(provider => provider.GetRequiredService<VivaInterviewStore>());
        services.AddScoped<GradingStore>();
        services.AddScoped<IGradingStore>(provider => provider.GetRequiredService<GradingStore>());
        services.AddScoped<MyVivaSessionsReader>();
        services.AddScoped<IMyVivaSessionsReader>(provider => provider.GetRequiredService<MyVivaSessionsReader>());
        services.AddScoped<ExamLiveReader>();
        services.AddScoped<IExamLiveReader>(provider => provider.GetRequiredService<ExamLiveReader>());
        services.AddScoped<StudentResultAccess>();
        services.AddScoped<IStudentResultAccess>(provider => provider.GetRequiredService<StudentResultAccess>());
        services.AddScoped<IQuestionFileReader, QuestionFileReader>();
        services.AddScoped<IDocumentTextExtractor, DocumentTextExtractor>();
        services.TryAddSingleton<IAnswerAudioStorage>(provider => new LocalAnswerAudioStorage(
            provider.GetRequiredService<IConfiguration>()["VivaAudio:RootDirectory"]));
        return services;
    }
}
