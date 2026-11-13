using AIpoweredVivaExamSystem.DataAccess;
using AIpoweredVivaExamSystem.DataAccess.Contracts;
using AIpoweredVivaExamSystem.DataAccess.Repositories;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AIpoweredVivaExamSystem.Business;

public static class ServiceRegistration
{
    // Presentation registers Business only; Business owns its downward reference to DataAccess.
    public static IServiceCollection AddBusiness(this IServiceCollection services)
    {
        services.AddDataAccess();
        services.AddLogging();
        services.AddOptions<JwtOptions>().BindConfiguration("Jwt");
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IDataActor>(provider => provider.GetRequiredService<ICurrentUser>());
        services.TryAddScoped<IExamNotifier>(_ => new NoExamNotifications());
        services.TryAddScoped<IStudentResultNotifier>(_ => new NoStudentResultNotifications());
        services.AddScoped<NotifyingExamUnitOfWork>(provider => new NotifyingExamUnitOfWork(
            provider.GetRequiredService<ExamUnitOfWork>(), provider.GetRequiredService<IExamNotifier>(),
            provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<NotifyingExamUnitOfWork>>()));
        services.AddScoped<IExamUnitOfWork>(provider => provider.GetRequiredService<NotifyingExamUnitOfWork>());

        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IdentityPasswordVerifier>();
        services.AddScoped<IPasswordVerifier>(provider => provider.GetRequiredService<IdentityPasswordVerifier>());
        services.AddScoped<JwtAccessTokenIssuer>();
        services.AddScoped<IAccessTokenIssuer>(provider => provider.GetRequiredService<JwtAccessTokenIssuer>());
        services.AddScoped<LoginService>();
        services.AddScoped<AccessService>();
        services.AddScoped<ExamAccessGuard>();
        services.AddScoped<ExamFoundationService>();
        services.AddScoped<SubjectService>();
        services.AddScoped<ISubjectService>(provider => provider.GetRequiredService<SubjectService>());
        services.AddScoped<TopicService>();
        services.AddScoped<ITopicService>(provider => provider.GetRequiredService<TopicService>());
        services.AddScoped<QuestionService>();
        services.AddScoped<IQuestionService>(provider => provider.GetRequiredService<QuestionService>());
        services.AddScoped<RubricService>();
        services.AddScoped<IRubricService>(provider => provider.GetRequiredService<RubricService>());
        services.AddScoped<ExamService>();
        services.AddScoped<IExamService>(provider => provider.GetRequiredService<ExamService>());
        services.AddScoped<ExamPoolService>();
        services.AddScoped<IExamPoolReader>(provider => provider.GetRequiredService<ExamPoolService>());
        services.AddScoped<ExamRosterService>();
        services.AddScoped<IExamRosterService>(provider => provider.GetRequiredService<ExamRosterService>());
        services.AddScoped<IExamRosterReader>(provider => provider.GetRequiredService<ExamRosterService>());
        services.AddScoped<ExamScheduleService>();
        services.AddScoped<IExamScheduleService>(provider => provider.GetRequiredService<ExamScheduleService>());
        services.AddScoped<IScheduleReader>(provider => provider.GetRequiredService<ExamScheduleService>());
        services.AddScoped<IScheduleReadiness>(provider => provider.GetRequiredService<ExamScheduleService>());
        services.AddScoped<VivaSessionService>();
        services.AddScoped<IVivaSessionService>(provider => provider.GetRequiredService<VivaSessionService>());
        services.AddScoped<VivaInterviewService>();
        services.AddScoped<IVivaInterviewService>(provider => provider.GetRequiredService<VivaInterviewService>());
        services.AddScoped<VivaQuestionSpeechService>();
        services.AddScoped<VivaScribeTokenService>();
        services.AddScoped<GradingService>();
        services.AddScoped<DocumentService>();
        services.AddScoped<QuestionImportService>();
        services.AddScoped<MyVivaSessionsService>();
        services.AddScoped<ExamLiveService>();
        services.AddScoped<QuestionBankReadService>();
        services.AddScoped<AnswerAudioService>();
        services.AddScoped<ExamRealtimeAccess>();
        services.AddScoped<StudentResultAccessService>();
        services.AddScoped<IStudentResultAuthorization>(provider => provider.GetRequiredService<StudentResultAccessService>());

        services.AddScoped<IValidator<CreateSubjectRequest>, CreateSubjectRequestValidator>();
        services.AddScoped<IValidator<UpdateSubjectRequest>, UpdateSubjectRequestValidator>();
        services.AddScoped<IValidator<SubjectListQuery>, SubjectListQueryValidator>();
        services.AddScoped<IValidator<CreateTopicRequest>, CreateTopicRequestValidator>();
        services.AddScoped<IValidator<UpdateTopicRequest>, UpdateTopicRequestValidator>();
        services.AddScoped<IValidator<TopicListQuery>, TopicListQueryValidator>();
        services.AddScoped<IValidator<CreateQuestionRequest>, CreateQuestionRequestValidator>();
        services.AddScoped<IValidator<UpdateQuestionRequest>, UpdateQuestionRequestValidator>();
        services.AddScoped<IValidator<QuestionListQuery>, QuestionListQueryValidator>();
        services.AddScoped<IValidator<CreateRubricRequest>, CreateRubricRequestValidator>();
        services.AddScoped<IValidator<UpdateRubricRequest>, UpdateRubricRequestValidator>();
        services.AddScoped<IValidator<RubricListQuery>, RubricListQueryValidator>();

        services.AddHttpClient<OpenAiQuestionGenerator>(client => client.Timeout = TimeSpan.FromSeconds(90));
        services.AddScoped<IQuestionGenerator>(provider => provider.GetRequiredService<OpenAiQuestionGenerator>());
        services.AddHttpClient<OpenAiVivaExaminer>(client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddScoped<IVivaAiExaminer>(provider => provider.GetRequiredService<OpenAiVivaExaminer>());
        services.AddHttpClient<ElevenLabsSpeechTranscriber>(client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddScoped<ISpeechTranscriber>(provider => provider.GetRequiredService<ElevenLabsSpeechTranscriber>());
        services.AddHttpClient<ElevenLabsScribeTokenProvider>(client => client.Timeout = TimeSpan.FromSeconds(12));
        services.AddScoped<IRealtimeScribeTokenProvider>(provider => provider.GetRequiredService<ElevenLabsScribeTokenProvider>());
        services.AddHttpClient<OpenAiRubricGrader>(client => client.Timeout = TimeSpan.FromSeconds(60));
        services.AddScoped<IAiRubricGrader>(provider => provider.GetRequiredService<OpenAiRubricGrader>());
        services.AddHttpClient("ElevenLabs", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(12);
            client.MaxResponseContentBufferSize = 4 * 1024 * 1024;
        });
        services.AddSingleton<ElevenLabsSpeechSynthesizer>();
        services.AddSingleton<IExaminerSpeechSynthesizer>(provider => provider.GetRequiredService<ElevenLabsSpeechSynthesizer>());
        return services;
    }

    private sealed class NoExamNotifications : IExamNotifier
    {
        public Task NotifyChangedAsync(Guid examId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class NoStudentResultNotifications : IStudentResultNotifier
    {
        public Task NotifyChangedAsync(Guid sessionId, CancellationToken ct) => Task.CompletedTask;
    }
}
