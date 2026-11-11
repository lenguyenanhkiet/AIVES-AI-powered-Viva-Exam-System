using System.Net;
using System.Text;
using System.Text.Json;
using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Application.VivaSessions;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Infrastructure.VivaSessions;
using AIpoweredVivaExamSystem.Persistence.Context;
using AIpoweredVivaExamSystem.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AIpoweredVivaExamSystem.Tests;

public sealed class VivaInterviewDomainTests
{
    [Fact]
    public void StudentAnswer_enforces_type_followup_level_and_answer_lifecycle_invariants()
    {
        var sessionQuestionId = Guid.NewGuid();
        var askedAt = DateTimeOffset.UtcNow;

        var mainTurn = new StudentAnswer(sessionQuestionId, AnswerType.Main, 0, "  Explain SOLID principles.  ", askedAt);
        Assert.Equal("Explain SOLID principles.", mainTurn.PromptText);
        Assert.Equal(AnswerType.Main, mainTurn.AnswerType);
        Assert.Equal(0, mainTurn.FollowUpLevel);
        Assert.Null(mainTurn.AnsweredAt);

        // Main turn must have FollowUpLevel == 0 and no parentAnswerId
        Assert.Throws<DomainValidationException>(() =>
            new StudentAnswer(sessionQuestionId, AnswerType.Main, 1, "Prompt", askedAt));

        // FollowUp turn must have FollowUpLevel >= 1 and a valid parentAnswerId
        Assert.Throws<DomainValidationException>(() =>
            new StudentAnswer(sessionQuestionId, AnswerType.FollowUp, 0, "Prompt", askedAt, mainTurn.Id));
        Assert.Throws<DomainValidationException>(() =>
            new StudentAnswer(sessionQuestionId, AnswerType.FollowUp, 1, "Prompt", askedAt, null));

        var followUpTurn = new StudentAnswer(sessionQuestionId, AnswerType.FollowUp, 1, "Give a concrete example of DIP.", askedAt, mainTurn.Id);
        Assert.Equal(AnswerType.FollowUp, followUpTurn.AnswerType);
        Assert.Equal(1, followUpTurn.FollowUpLevel);
        Assert.Equal(mainTurn.Id, followUpTurn.ParentAnswerId);

        // AnsweredAt cannot be before AskedAt
        Assert.Throws<DomainValidationException>(() =>
            followUpTurn.SubmitResponse("Valid transcript", askedAt.AddSeconds(-5)));

        // Submit response succeeds once and trims transcript
        var answeredAt = askedAt.AddSeconds(25);
        followUpTurn.SubmitResponse("  High-level modules depend on abstractions.  ", answeredAt);
        Assert.Equal("High-level modules depend on abstractions.", followUpTurn.Transcript);
        Assert.Equal(answeredAt, followUpTurn.AnsweredAt);

        // Cannot overwrite an already answered turn
        Assert.Throws<DomainValidationException>(() =>
            followUpTurn.SubmitResponse("Second attempt", answeredAt.AddSeconds(5)));
    }

    [Fact]
    public void AnswerAnalysis_and_AnswerAudio_enforce_domain_invariants()
    {
        var answerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var analysis = new AnswerAnalysis(answerId, false, "  LSP, Invariant  ", "  Missing Liskov Substitution example  ", "  Explain LSP  ", "gpt-4o-mini", now);
        Assert.Equal("Missing Liskov Substitution example", analysis.AnalysisSummary);
        Assert.Equal("LSP, Invariant", analysis.MissingConcepts);
        Assert.Equal("Explain LSP", analysis.FollowUpPrompt);
        Assert.False(analysis.IsComplete);

        // Complete analysis clears FollowUpPrompt automatically
        var completeAnalysis = new AnswerAnalysis(answerId, true, null, "Complete", "Should be cleared", "gpt-4o-mini", now);
        Assert.Null(completeAnalysis.FollowUpPrompt);

        var audio = new AnswerAudio(answerId, "viva-audios/session/turn.webm", "audio/webm", 12_345, "abc123hash", 18, now);
        Assert.Equal("viva-audios/session/turn.webm", audio.FilePath);
        Assert.Equal("audio/webm", audio.MimeType);
        Assert.Equal(12_345, audio.FileSizeBytes);
        Assert.Equal(18, audio.DurationSeconds);

        // Oversized or 0-byte file rejected
        Assert.Throws<DomainValidationException>(() =>
            new AnswerAudio(answerId, "path.webm", "audio/webm", 0, "hash", 5, now));
        Assert.Throws<DomainValidationException>(() =>
            new AnswerAudio(answerId, "path.webm", "audio/webm", 25 * 1024 * 1024, "hash", 5, now));
    }

    [Fact]
    public void CalculateTurnLimitSeconds_allocates_fair_bounds_from_exam_configuration()
    {
        // 15 minutes (900s), 3 main questions, max 2 follow-ups => 3 * (1 + 2) = 9 turns => 100s per turn
        Assert.Equal(100, VivaInterviewService.CalculateTurnLimitSeconds(15, 3, 2));
        // Clamped between 30s and 300s
        Assert.Equal(30, VivaInterviewService.CalculateTurnLimitSeconds(1, 5, 3));
        Assert.Equal(300, VivaInterviewService.CalculateTurnLimitSeconds(60, 1, 0));
    }
}

public sealed class VivaExaminerUnitTests
{
    private static ExaminerEvaluationContext SampleContext(string currentAnswer, int followUpLevel = 0, int maxFollowUps = 2)
    {
        var rubricJson = """
        {
          "Title": "DI Rubric",
          "Criteria": [
            {
              "Name": "Inversion of Control",
              "ExpectedConcepts": "IoC container, interface abstraction, Constructor Injection"
            }
          ]
        }
        """;

        return new ExaminerEvaluationContext(
            SubjectName: "PRN222",
            MainQuestionContent: "Giải thích cơ chế Dependency Injection trong ASP.NET Core.",
            RubricSnapshotJson: rubricJson,
            CurrentFollowUpLevel: followUpLevel,
            MaxFollowUpPerQuestion: maxFollowUps,
            History:
            [
                new ConversationTurn(AnswerType.Main, 0, "Giải thích cơ chế Dependency Injection trong ASP.NET Core.", currentAnswer)
            ]);
    }

    [Fact]
    public void FallbackEvaluator_detects_missing_concepts_and_generates_adaptive_followup()
    {
        var incompleteCtx = SampleContext(
            "Dependency Injection là kỹ thuật giúp truyền đối tượng vào class.",
            followUpLevel: 0,
            maxFollowUps: 2);

        var decision = OpenAiVivaExaminer.EvaluateByRubricFallback(incompleteCtx);
        Assert.False(decision.IsComplete);
        Assert.NotNull(decision.MissingConcepts);
        Assert.NotNull(decision.FollowUpPrompt);

        // When all expected concepts are covered with sufficient depth, IsComplete = true and no follow-up is needed
        var completeCtx = SampleContext(
            "Trong ASP.NET Core, Dependency Injection hoạt động thông qua IoC container tích hợp sẵn, cho phép đăng ký dịch vụ theo interface abstraction và tiêm phụ thuộc qua Constructor Injection để giảm sự phụ thuộc chặt giữa các tầng.",
            followUpLevel: 0,
            maxFollowUps: 2);

        var completeDecision = OpenAiVivaExaminer.EvaluateByRubricFallback(completeCtx);
        Assert.True(completeDecision.IsComplete);
        Assert.Null(completeDecision.FollowUpPrompt);
    }

    [Fact]
    public async Task OpenAiVivaExaminer_parses_structured_json_response()
    {
        var aiPayload = """
        {
          "choices": [
            {
              "message": {
                "content": "{\"isComplete\":false,\"analysisSummary\":\"Thiếu giải thích về vòng đời Scoped và Transient.\",\"missingConcepts\":\"Scoped, Transient\",\"followUpPrompt\":\"Bạn hãy phân biệt sự khác nhau giữa vòng đời Scoped và Transient trong DI container?\"}"
              }
            }
          ]
        }
        """;

        using var http = new HttpClient(new StubHandler(aiPayload));
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:ApiKey"] = "test-key",
                ["Ai:Model"] = "gpt-4o-mini"
            })
            .Build();
        var examiner = new OpenAiVivaExaminer(http, config);

        var decision = await examiner.EvaluateAndFollowUpAsync(SampleContext("Trả lời sơ lược", followUpLevel: 0, maxFollowUps: 2), default);
        Assert.False(decision.IsComplete);
        Assert.Equal("Scoped, Transient", decision.MissingConcepts);
        Assert.Equal("Bạn hãy phân biệt sự khác nhau giữa vòng đời Scoped và Transient trong DI container?", decision.FollowUpPrompt);
    }

    private sealed class StubHandler(string responseBody, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
    }
}

[Collection("SQL Server")]
public sealed class VivaInterviewSqlTests(SqlServerFixture fixture)
{
    private sealed record Actor(Guid Id, bool IsAdministrator = false, bool IsLecturer = false) : ICurrentUser;

    private sealed class Notifier : IExamNotifier
    {
        public int Calls;
        public Task NotifyChangedAsync(Guid id, CancellationToken ct)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeExaminer(Func<ExaminerEvaluationContext, ExaminerDecision> decide) : IVivaAiExaminer
    {
        public Task<ExaminerDecision> EvaluateAndFollowUpAsync(ExaminerEvaluationContext context, CancellationToken ct) =>
            Task.FromResult(decide(context));
    }

    private sealed class FakeTranscriber(string text = "Transcribed from server audio") : ISpeechTranscriber
    {
        public Task<string> TranscribeAsync(byte[] audioBytes, string fileName, string language, string? promptHint, CancellationToken ct) =>
            Task.FromResult(text);
    }

    private sealed class FakeAudioStorage : IAnswerAudioStorage
    {
        public Task<string> SaveAudioAsync(Guid sessionId, Guid turnId, byte[] content, string mimeType, CancellationToken ct) =>
            Task.FromResult($"viva-audios/{sessionId:N}/{turnId:N}.webm");
    }

    private ApplicationDbContext Context(Actor actor) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(fixture.ConnectionString).Options,
        actor);

    private static IVivaInterviewService CreateInterviewService(
        ApplicationDbContext db,
        Actor actor,
        Notifier notifier,
        IVivaAiExaminer examiner,
        ISpeechTranscriber? transcriber = null)
    {
        var uow = new ExamUnitOfWork(db, notifier, NullLogger<ExamUnitOfWork>.Instance);
        var sessionStore = new VivaSessionStore(db, uow, actor);
        var guard = new ExamAccessGuard(new ExamFoundationStore(db), actor);
        var interviewStore = new VivaInterviewStore(db, uow, examiner, new FakeAudioStorage(), actor);
        return new VivaInterviewService(
            sessionStore,
            interviewStore,
            transcriber ?? new FakeTranscriber(),
            guard,
            actor);
    }

    private async Task<ExamDemo> SeedOngoingExamAsync(int mainQuestions = 2, int maxFollowUps = 1)
    {
        await using var db = fixture.CreateContext();
        var demo = await ExamDemo.SeedAsync(db);
        db.Entry(demo.Exam).Property(e => e.StartAt).CurrentValue = DateTimeOffset.UtcNow.AddHours(-1);
        db.Entry(demo.Exam).Property(e => e.EndAt).CurrentValue = DateTimeOffset.UtcNow.AddHours(2);
        db.Entry(demo.Exam).Property(e => e.NumberOfMainQuestions).CurrentValue = mainQuestions;
        db.Entry(demo.Exam).Property(e => e.MaxFollowUpPerQuestion).CurrentValue = maxFollowUps;
        db.Entry(demo.Exam).Property(e => e.Status).CurrentValue = ExamStatus.Ongoing;
        demo.Enrollment.Schedule(DateTimeOffset.UtcNow.AddMinutes(-2));

        for (var i = 0; i < 4; i++)
        {
            var q = new Question(demo.Subject.Id, $"Main question #{i + 1}", BloomLevel.Understand,
                QuestionDifficulty.Medium, "SECRET EXPECTED ANSWER") { CreatedBy = demo.Lecturer.Id };
            q.Approve();
            db.AddRange(
                q,
                new ExamQuestionPool(demo.Exam.Id, q.Id),
                new Rubric(q.Id, "SECRET RUBRIC TITLE", null,
                    [new CriterionDefinition(null, "Core concept", null, "SECRET CONCEPT", 10, 1)]));
        }

        await db.SaveChangesAsync();
        return demo;
    }

    [Fact]
    public async Task AdaptiveInterviewFlow_GeneratesFollowUp_AdvancesMainQuestion_AndCompletesSession()
    {
        var demo = await SeedOngoingExamAsync(mainQuestions: 2, maxFollowUps: 1);
        var actor = new Actor(demo.Student.Id);
        var notifier = new Notifier();

        // Examiner asks 1 follow-up on Question 1 (incomplete), then marks complete on Question 2
        var examiner = new FakeExaminer(ctx =>
        {
            var lastTranscript = ctx.History.LastOrDefault()?.Transcript ?? "";
            if (ctx.CurrentFollowUpLevel == 0 && lastTranscript.Contains("Mơ hồ"))
            {
                return new ExaminerDecision(
                    IsComplete: false,
                    MissingConcepts: "SECRET MISSING CONCEPT",
                    AnalysisSummary: "SECRET ANALYSIS SUMMARY",
                    FollowUpPrompt: "Bạn hãy làm rõ hơn ví dụ thực tế cho câu số 1?",
                    ModelName: "test-model");
            }

            return new ExaminerDecision(
                IsComplete: true,
                MissingConcepts: null,
                AnalysisSummary: "Trả lời đầy đủ ý.",
                FollowUpPrompt: null,
                ModelName: "test-model");
        });

        VivaInterviewState state;
        await using (var db = Context(actor))
        {
            var service = CreateInterviewService(db, actor, notifier, examiner);
            state = await service.OpenOrResumeByEnrollmentAsync(demo.Enrollment.Id, default);
        }

        Assert.Null(state.CompletedAt);
        Assert.Equal(1, state.CurrentMainPosition);
        Assert.Equal(2, state.TotalMainQuestions);
        Assert.Equal(0, state.CurrentFollowUpLevel);
        Assert.Equal(1, state.MaxFollowUpPerQuestion);
        Assert.NotNull(state.ActiveTurn);
        Assert.Equal(AnswerType.Main, state.ActiveTurn!.AnswerType);
        Assert.DoesNotContain("SECRET", JsonSerializer.Serialize(state));

        // Turn 1: Student gives an incomplete answer with audio -> triggers Follow-up #1 on Question 1
        await using (var db = Context(actor))
        {
            var service = CreateInterviewService(db, actor, notifier, examiner);
            state = await service.SubmitAnswerAsync(new SubmitAnswerRequest(
                SessionId: state.SessionId,
                TurnId: state.ActiveTurn.TurnId,
                Transcript: "Câu trả lời còn Mơ hồ chưa rõ ý.",
                AudioBytes: [1, 2, 3, 4],
                AudioMimeType: "audio/webm",
                AudioDurationSeconds: 12), default);
        }

        Assert.Null(state.CompletedAt);
        Assert.Equal(1, state.CurrentMainPosition);
        Assert.Equal(1, state.CurrentFollowUpLevel);
        Assert.NotNull(state.ActiveTurn);
        Assert.Equal(AnswerType.FollowUp, state.ActiveTurn!.AnswerType);
        Assert.Equal(1, state.ActiveTurn.FollowUpLevel);
        Assert.Equal("Bạn hãy làm rõ hơn ví dụ thực tế cho câu số 1?", state.ActiveTurn.PromptText);
        Assert.True(state.Turns.First(t => t.AnsweredAt.HasValue).HasAudio);
        Assert.DoesNotContain("SECRET", JsonSerializer.Serialize(state));

        // Turn 2: Student answers Follow-up #1 -> reaches MaxFollowUpPerQuestion (1) -> advances to Main Question #2
        await using (var db = Context(actor))
        {
            var service = CreateInterviewService(db, actor, notifier, examiner);
            state = await service.SubmitAnswerAsync(new SubmitAnswerRequest(
                SessionId: state.SessionId,
                TurnId: state.ActiveTurn.TurnId,
                Transcript: "Em bổ sung ví dụ chi tiết cho câu 1."), default);
        }

        Assert.Null(state.CompletedAt);
        Assert.Equal(2, state.CurrentMainPosition);
        Assert.Equal(0, state.CurrentFollowUpLevel);
        Assert.NotNull(state.ActiveTurn);
        Assert.Equal(AnswerType.Main, state.ActiveTurn!.AnswerType);
        Assert.Equal(2, state.ActiveTurn.MainPosition);

        // Turn 3: Student gives a complete answer to Main Question #2 (skips follow-up & auto-completes session)
        await using (var db = Context(actor))
        {
            var service = CreateInterviewService(db, actor, notifier, examiner);
            state = await service.SubmitAnswerAsync(new SubmitAnswerRequest(
                SessionId: state.SessionId,
                TurnId: state.ActiveTurn.TurnId,
                Transcript: "Câu trả lời đầy đủ tất cả các ý của câu 2."), default);
        }

        Assert.NotNull(state.CompletedAt);
        Assert.Null(state.ActiveTurn);
        Assert.Equal(3, state.Turns.Count);
        Assert.All(state.Turns, t => Assert.NotNull(t.AnsweredAt));

        // Verify internal grading analysis and audio record were persisted in SQL Server for lecturer review
        var turnIds = state.Turns.Select(t => t.TurnId).ToArray();
        await using (var verifyDb = fixture.CreateContext())
        {
            var analyses = await verifyDb.AnswerAnalyses.IgnoreQueryFilters()
                .Where(a => turnIds.Contains(a.StudentAnswerId))
                .ToListAsync();
            Assert.Equal(3, analyses.Count);
            Assert.Contains(analyses, a => a.MissingConcepts == "SECRET MISSING CONCEPT" && !a.IsComplete);

            var audios = await verifyDb.AnswerAudios.IgnoreQueryFilters()
                .Where(a => turnIds.Contains(a.StudentAnswerId))
                .ToListAsync();
            Assert.Single(audios);
        }
    }

    [Fact]
    public async Task SubmitAnswer_UsesServerTranscriberWhenTranscriptIsBlankAndAudioProvided_AndBlocksOtherStudent()
    {
        var demo = await SeedOngoingExamAsync(mainQuestions: 1, maxFollowUps: 0);
        var owner = new Actor(demo.Student.Id);
        var notifier = new Notifier();
        var examiner = new FakeExaminer(_ => new ExaminerDecision(true, null, "Good", null, "test-model"));

        VivaInterviewState state;
        await using (var db = Context(owner))
        {
            var service = CreateInterviewService(db, owner, notifier, examiner);
            state = await service.OpenOrResumeByEnrollmentAsync(demo.Enrollment.Id, default);
        }

        // Another student cannot open or submit to this session
        var intruder = new Actor(Guid.NewGuid());
        await using (var intruderDb = Context(intruder))
        {
            var intruderService = CreateInterviewService(intruderDb, intruder, notifier, examiner);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                intruderService.OpenOrResumeByEnrollmentAsync(demo.Enrollment.Id, default));
        }

        // Submitting empty transcript with audio uses ISpeechTranscriber fallback automatically
        await using (var db = Context(owner))
        {
            var service = CreateInterviewService(
                db,
                owner,
                notifier,
                examiner,
                new FakeTranscriber("Nội dung được Whisper STT nhận diện tự động"));

            state = await service.SubmitAnswerAsync(new SubmitAnswerRequest(
                SessionId: state.SessionId,
                TurnId: state.ActiveTurn!.TurnId,
                Transcript: "   ",
                AudioBytes: [10, 20, 30],
                AudioMimeType: "audio/webm",
                AudioDurationSeconds: 5), default);
        }

        Assert.NotNull(state.CompletedAt);
        Assert.Equal("Nội dung được Whisper STT nhận diện tự động", Assert.Single(state.Turns).Transcript);
    }
}
