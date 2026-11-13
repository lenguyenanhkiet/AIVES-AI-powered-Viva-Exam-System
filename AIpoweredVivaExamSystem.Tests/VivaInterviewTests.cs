using System.Net;
using System.Text;
using System.Text.Json;
using AIpoweredVivaExamSystem.Business.Authentication;
using AIpoweredVivaExamSystem.Business.Exams;
using AIpoweredVivaExamSystem.Business.VivaSessions;
using AIpoweredVivaExamSystem.DataAccess.Contracts.VivaSessions;
using AIpoweredVivaExamSystem.DataAccess.Models.VivaSessions;
using AIpoweredVivaExamSystem.DataAccess.Common;
using AIpoweredVivaExamSystem.DataAccess.Entities;
using AIpoweredVivaExamSystem.DataAccess.Enums;
using AIpoweredVivaExamSystem.Business.Integrations.VivaSessions;
using AIpoweredVivaExamSystem.DataAccess.Context;
using AIpoweredVivaExamSystem.DataAccess.Repositories;
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
    public async Task OpenAiVivaExaminer_retries_transient_failure_and_parses_structured_json_response()
    {
        var aiPayload = """
        {
          "choices": [
            {
              "finish_reason": "stop",
              "message": {
                "content": "{\"isComplete\":false,\"analysisSummary\":\"Thiếu giải thích về vòng đời Scoped và Transient.\",\"missingConcepts\":\"Scoped, Transient\",\"followUpPrompt\":\"Bạn hãy phân biệt sự khác nhau giữa vòng đời Scoped và Transient trong DI container?\"}"
              }
            }
          ]
        }
        """;

        var handler = new StubHandler(aiPayload, transientFailures: 1);
        using var http = new HttpClient(handler);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:ApiKey"] = "test-key",
                ["Ai:Model"] = " "
            })
            .Build();
        var examiner = new OpenAiVivaExaminer(http, config);

        var decision = await examiner.EvaluateAndFollowUpAsync(SampleContext("Trả lời sơ lược", followUpLevel: 0, maxFollowUps: 2), default);
        Assert.Equal(2, handler.Calls);
        Assert.False(decision.IsComplete);
        Assert.Equal("Scoped, Transient", decision.MissingConcepts);
        Assert.Equal("Bạn hãy phân biệt sự khác nhau giữa vòng đời Scoped và Transient trong DI container?", decision.FollowUpPrompt);
    }

    [Fact]
    public async Task OpenAiVivaExaminer_stops_after_bounded_transient_failures()
    {
        var handler = new StubHandler("{}", transientFailures: int.MaxValue);
        using var http = new HttpClient(handler);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ai:ApiKey"] = "test-key",
            ["Ai:Model"] = "glm-5.3-flash"
        }).Build();
        var examiner = new OpenAiVivaExaminer(http, config);

        await Assert.ThrowsAsync<AIpoweredVivaExamSystem.DataAccess.Models.Common.ResourceConflictException>(() =>
            examiner.EvaluateAndFollowUpAsync(SampleContext("Incomplete"), default));
        Assert.InRange(handler.Calls, 2, 3);
    }

    [Fact]
    public async Task Scribe_retries_without_losing_the_recorded_audio()
    {
        var handler = new StubHandler("{\"text\":\"Transcribed answer\"}", transientFailures: 1);
        using var http = new HttpClient(handler);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ElevenLabs:ApiKey"] = "test-key"
        }).Build();

        var transcript = await new ElevenLabsSpeechTranscriber(http, config)
            .TranscribeAsync([0, 1, 2, 255], "answer.webm", "vi", null, default);

        Assert.Equal(2, handler.Calls);
        Assert.False(string.IsNullOrWhiteSpace(transcript));
    }

    [Fact]
    public async Task Retry_backoff_shares_the_request_timeout_budget()
    {
        var handler = new StubHandler("{}", transientFailures: int.MaxValue);
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(50) };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ai:ApiKey"] = "test-key"
        }).Build();
        await Assert.ThrowsAsync<AIpoweredVivaExamSystem.DataAccess.Models.Common.ResourceConflictException>(() =>
            new OpenAiVivaExaminer(http, config).EvaluateAndFollowUpAsync(SampleContext("Incomplete"), default));
        Assert.Equal(1, handler.Calls);
    }

    private sealed class StubHandler(string responseBody, int transientFailures = 0) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        private string? firstBody;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            firstBody ??= body;
            Assert.Equal(firstBody, body);
            Calls++;
            return new HttpResponseMessage(Calls <= transientFailures ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
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

    private sealed class FakeTranscriber(string text = "Transcribed from server audio", bool unavailable = false) : ISpeechTranscriber
    {
        public Task<string> TranscribeAsync(byte[] audioBytes, string fileName, string language, string? promptHint, CancellationToken ct) =>
            unavailable ? throw new AIpoweredVivaExamSystem.DataAccess.Models.Common.ResourceConflictException("Use browser recognition or type.") : Task.FromResult(text);
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
        var uow = new NotifyingExamUnitOfWork(new ExamUnitOfWork(db), notifier);
        var sessionStore = new VivaSessionStore(db, uow, actor);
        var guard = new ExamAccessGuard(new ExamFoundationStore(db), actor);
        var interviewStore = new VivaInterviewStore(db, uow, new FakeAudioStorage(), actor);
        return new VivaInterviewService(
            sessionStore,
            interviewStore,
            transcriber ?? new FakeTranscriber(),
            guard,
            actor,
            examiner);
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
    public async Task Invalid_recording_is_rejected_before_examiner_without_consuming_the_turn()
    {
        var demo = await SeedOngoingExamAsync(mainQuestions: 1, maxFollowUps: 1);
        var actor = new Actor(demo.Student.Id);
        await using var db = Context(actor);
        var service = CreateInterviewService(db, actor, new Notifier(),
            new FakeExaminer(_ => throw new InvalidOperationException("Invalid recordings must not reach the examiner.")));
        var state = await service.OpenOrResumeByEnrollmentAsync(demo.Enrollment.Id, default);
        var turnId = state.ActiveTurn!.TurnId;

        await Assert.ThrowsAsync<DomainValidationException>(() => service.SubmitAnswerAsync(
            new(state.SessionId, turnId, "An answer with an invalid recording.", [1, 2, 3], "text/plain"), default));

        var unchanged = await service.GetStateAsync(state.SessionId, default);
        Assert.Equal(turnId, unchanged.ActiveTurn!.TurnId);
        Assert.Null(unchanged.ActiveTurn.AnsweredAt);
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
            Assert.True(ctx.CurrentFollowUpLevel < ctx.MaxFollowUpPerQuestion,
                "The examiner must not be called when another follow-up is forbidden.");
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
                AudioDurationSeconds: 12, TimedOut: true), default);
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

    private sealed class ConcurrentExaminer : IVivaAiExaminer
    {
        public int Calls;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<ExaminerDecision> EvaluateAndFollowUpAsync(ExaminerEvaluationContext context, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls); Entered.TrySetResult();
            await Release.Task.WaitAsync(ct);
            return new ExaminerDecision(true, null, null, null, "test-model");
        }
    }

    [Fact]
    public async Task Answer_without_available_followups_is_stored_and_completes_without_the_examiner()
    {
        var demo = await SeedOngoingExamAsync(mainQuestions: 1, maxFollowUps: 0);
        var actor = new Actor(demo.Student.Id);
        await using var db = Context(actor);
        var service = CreateInterviewService(db, actor, new Notifier(),
            new FakeExaminer(_ => throw new InvalidOperationException("There is no follow-up decision to make.")));
        var state = await service.OpenOrResumeByEnrollmentAsync(demo.Enrollment.Id, default);
        var turnId = state.ActiveTurn!.TurnId;
        state = await service.SubmitAnswerAsync(new(state.SessionId, turnId, "The captured answer.", [1,2,3], "audio/webm"), default);
        Assert.NotNull(state.CompletedAt);
        var turn = Assert.Single(state.Turns);
        Assert.Equal("The captured answer.", turn.Transcript);
        Assert.True(turn.HasAudio);
        Assert.False(turn.TimedOut);
        Assert.Null(state.Result);
    }

    [Fact]
    public async Task Concurrent_submits_of_the_same_turn_commit_one_answer_and_one_advance()
    {
        var demo = await SeedOngoingExamAsync(mainQuestions: 1, maxFollowUps: 1);
        var actor = new Actor(demo.Student.Id);
        var examiner = new ConcurrentExaminer();
        await using var firstDb = Context(actor);
        await using var secondDb = Context(actor);
        var firstService = CreateInterviewService(firstDb, actor, new Notifier(), examiner);
        var secondService = CreateInterviewService(secondDb, actor, new Notifier(), examiner);
        var state = await firstService.OpenOrResumeByEnrollmentAsync(demo.Enrollment.Id, default);
        var request = new SubmitAnswerRequest(state.SessionId, state.ActiveTurn!.TurnId, "Answer");
        var first = firstService.SubmitAnswerAsync(request, default);
        Task<VivaInterviewState>? second = null;
        try
        {
            await examiner.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            second = secondService.SubmitAnswerAsync(request, default);
        }
        finally { examiner.Release.TrySetResult(); }
        var results = await Task.WhenAll(first, second!);
        Assert.All(results, result => Assert.NotNull(result.CompletedAt));
        Assert.Equal(1, examiner.Calls);
        await using var verify = fixture.CreateContext();
        Assert.Equal(1, await verify.StudentAnswers.CountAsync(a => a.Id == request.TurnId && a.AnsweredAt != null));
        Assert.Equal(1, await verify.AnswerAnalyses.CountAsync(a => a.StudentAnswerId == request.TurnId));
    }

    [Fact]
    public async Task Examiner_runs_outside_the_exam_transaction()
    {
        var demo = await SeedOngoingExamAsync(mainQuestions: 1, maxFollowUps: 1);
        var actor = new Actor(demo.Student.Id);
        await using var db = Context(actor);
        var examiner = new FakeExaminer(_ => {
            Assert.Null(db.Database.CurrentTransaction);
            Assert.Null(System.Transactions.Transaction.Current);
            return new ExaminerDecision(true, null, null, null, "test-model");
        });
        var service = CreateInterviewService(db, actor, new Notifier(), examiner);
        var state = await service.OpenOrResumeByEnrollmentAsync(demo.Enrollment.Id, default);
        state = await service.SubmitAnswerAsync(new SubmitAnswerRequest(state.SessionId, state.ActiveTurn!.TurnId, "Answer"), default);
        Assert.NotNull(state.CompletedAt);
    }

    [Fact]
    public async Task Expired_turn_records_audio_without_answer_or_examiner_and_advances()
    {
        var demo = await SeedOngoingExamAsync(mainQuestions: 2, maxFollowUps: 1);
        var actor = new Actor(demo.Student.Id);
        var examiner = new FakeExaminer(_ => throw new InvalidOperationException("Expired answers must bypass AI."));
        var notifier = new Notifier();
        await using var db = Context(actor);
        var service = CreateInterviewService(db, actor, notifier, examiner, new FakeTranscriber(unavailable: true));
        var state = await service.OpenOrResumeByEnrollmentAsync(demo.Enrollment.Id, default);
        var expired = await db.StudentAnswers.IgnoreQueryFilters().SingleAsync(a => a.Id == state.ActiveTurn!.TurnId);
        db.Entry(expired).Property(a => a.AskedAt).CurrentValue = DateTimeOffset.UtcNow.AddSeconds(-state.TurnLimitSeconds);
        await db.SaveChangesAsync();

        state = await service.SubmitAnswerAsync(new SubmitAnswerRequest(
            state.SessionId, expired.Id, " ", [1, 2, 3], "audio/webm"), default);

        var recorded = state.Turns.Single(t => t.TurnId == expired.Id);
        Assert.NotNull(recorded.AnsweredAt);
        Assert.Null(recorded.Transcript);
        Assert.True(recorded.TimedOut);
        Assert.False(string.IsNullOrWhiteSpace(expired.Transcript));
        Assert.True(recorded.HasAudio);
        Assert.Equal(2, state.CurrentMainPosition);
        Assert.Equal(AnswerType.Main, state.ActiveTurn!.AnswerType);
        Assert.DoesNotContain(state.Turns, t => t.AnswerType == AnswerType.FollowUp);

        // An honest browser flag within the grace band keeps the captured answer and also skips AI.
        var finalTurn = await db.StudentAnswers.IgnoreQueryFilters().SingleAsync(a => a.Id == state.ActiveTurn!.TurnId);
        db.Entry(finalTurn).Property(a => a.AskedAt).CurrentValue = DateTimeOffset.UtcNow.AddSeconds(-state.TurnLimitSeconds + 1);
        await db.SaveChangesAsync();
        var captured = "Student's answer captured before the countdown ended.";
        state = await service.SubmitAnswerAsync(new SubmitAnswerRequest(
            state.SessionId, finalTurn.Id, captured, TimedOut: true), default);
        var finalAnswer = state.Turns.Single(t => t.TurnId == finalTurn.Id);
        Assert.True(finalAnswer.TimedOut);
        Assert.Equal(captured, finalAnswer.Transcript);
        Assert.NotNull(state.CompletedAt);
    }

    [Fact]
    public async Task Late_turn_discards_unearned_content_and_still_advances()
    {
        var demo = await SeedOngoingExamAsync();
        var actor = new Actor(demo.Student.Id);
        await using var db = Context(actor);
        var service = CreateInterviewService(db, actor, new Notifier(),
            new FakeExaminer(_ => throw new InvalidOperationException("Late answers must bypass AI.")));
        var state = await service.OpenOrResumeByEnrollmentAsync(demo.Enrollment.Id, default);
        var turn = await db.StudentAnswers.IgnoreQueryFilters().SingleAsync(a => a.Id == state.ActiveTurn!.TurnId);
        db.Entry(turn).Property(a => a.AskedAt).CurrentValue = DateTimeOffset.UtcNow.AddSeconds(-state.TurnLimitSeconds - 10);
        await db.SaveChangesAsync();

        state = await service.SubmitAnswerAsync(new SubmitAnswerRequest(
            state.SessionId, turn.Id, "Content produced after time expired", [1, 2, 3], "audio/webm"), default);

        var recorded = state.Turns.Single(t => t.TurnId == turn.Id);
        Assert.NotNull(recorded.AnsweredAt);
        Assert.NotEqual("Content produced after time expired", recorded.Transcript);
        Assert.False(recorded.HasAudio);
        Assert.Equal(2, state.CurrentMainPosition);
        Assert.Equal(AnswerType.Main, state.ActiveTurn!.AnswerType);
    }

    [Fact]
    public async Task Expired_slot_accepts_empty_submission_and_completes_without_another_question()
    {
        var demo = await SeedOngoingExamAsync();
        var actor = new Actor(demo.Student.Id);
        await using var db = Context(actor);
        var service = CreateInterviewService(db, actor, new Notifier(),
            new FakeExaminer(_ => throw new InvalidOperationException("Expired slots must bypass AI.")));
        var state = await service.OpenOrResumeByEnrollmentAsync(demo.Enrollment.Id, default);
        var enrollment = await db.ExamStudents.IgnoreQueryFilters().SingleAsync(e => e.Id == demo.Enrollment.Id);
        db.Entry(enrollment).Property(e => e.ScheduledAt).CurrentValue = DateTimeOffset.UtcNow.AddMinutes(-demo.Exam.DurationMinutes).AddSeconds(-10);
        await db.SaveChangesAsync();

        state = await service.SubmitAnswerAsync(new SubmitAnswerRequest(
            state.SessionId, state.ActiveTurn!.TurnId, null, TimedOut: true), default);

        Assert.NotNull(state.CompletedAt);
        Assert.Null(state.ActiveTurn);
        Assert.NotNull(Assert.Single(state.Turns).AnsweredAt);
    }

    private sealed class ForbiddenTokens : IRealtimeScribeTokenProvider
    {
        public Task<ScribeToken?> CreateAsync(string language, CancellationToken ct) =>
            throw new InvalidOperationException("Unauthorized requests must not obtain a token.");
    }

    private sealed class ForbiddenSpeech : IExaminerSpeechSynthesizer
    {
        public Task<byte[]?> SynthesizeAsync(string text, CancellationToken ct) =>
            throw new InvalidOperationException("Unauthorized requests must not reach speech generation or cache.");
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
            var speech = new VivaQuestionSpeechService(intruderService, new ForbiddenSpeech());
            await Assert.ThrowsAsync<AIpoweredVivaExamSystem.DataAccess.Models.Common.ResourceNotFoundException>(() =>
                speech.SpeakAsync(state.SessionId, state.ActiveTurn!.TurnId, default));
            var tokens = new VivaScribeTokenService(intruderService, new ForbiddenTokens());
            await Assert.ThrowsAsync<AIpoweredVivaExamSystem.DataAccess.Models.Common.ResourceNotFoundException>(() =>
                tokens.CreateAsync(state.SessionId, "vi", default));
        }

        // Missing speech configuration must fail a manual audio-only submit without consuming the turn.
        await using (var db = Context(owner))
        {
            using var http = new HttpClient();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:ApiKey"] = "test-key"
            }).Build();
            var service = CreateInterviewService(db, owner, notifier, examiner, new ElevenLabsSpeechTranscriber(http, config));
            await Assert.ThrowsAsync<AIpoweredVivaExamSystem.DataAccess.Models.Common.ResourceConflictException>(() =>
                service.SubmitAnswerAsync(new SubmitAnswerRequest(state.SessionId, state.ActiveTurn!.TurnId, null, [1, 2, 3], "audio/webm"), default));
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
