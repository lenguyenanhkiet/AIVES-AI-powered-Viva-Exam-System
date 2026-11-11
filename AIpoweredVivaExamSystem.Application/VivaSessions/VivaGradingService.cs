using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Domain.Common;

namespace AIpoweredVivaExamSystem.Application.VivaSessions;

public sealed class VivaGradingService(
    IVivaGradingStore store,
    IVivaSessionStore sessionStore,
    ExamAccessGuard guard,
    ICurrentUser actor) : IVivaGradingService
{
    public async Task<ExamGradingOverview> GetExamOverviewAsync(Guid examId, CancellationToken ct)
    {
        DomainRules.RequiredId(examId, nameof(examId));
        await guard.CanManageExamAsync(examId, ct);
        return await store.GetExamOverviewAsync(examId, ct);
    }

    public async Task<VivaSessionGradingSheet> GetSessionSheetAsync(Guid sessionId, bool autoGenerateMissingAi, CancellationToken ct)
    {
        DomainRules.RequiredId(sessionId, nameof(sessionId));
        var examId = await store.FindExamIdBySessionAsync(sessionId, ct);
        await guard.CanManageExamAsync(examId, ct);

        if (autoGenerateMissingAi)
        {
            return await store.GenerateAiGradingsAsync(sessionId, forceRegrade: false, ct);
        }

        return await store.GetSessionGradingSheetAsync(sessionId, ct);
    }

    public async Task<VivaSessionGradingSheet> RunAiGradingAsync(Guid sessionId, bool forceRegrade, CancellationToken ct)
    {
        DomainRules.RequiredId(sessionId, nameof(sessionId));
        var examId = await store.FindExamIdBySessionAsync(sessionId, ct);
        await guard.CanManageExamAsync(examId, ct);
        return await store.GenerateAiGradingsAsync(sessionId, forceRegrade, ct);
    }

    public async Task<VivaSessionGradingSheet> SaveTeacherReviewAsync(SaveTeacherReviewRequest request, CancellationToken ct)
    {
        DomainRules.RequiredId(request.SessionId, nameof(request.SessionId));
        DomainRules.RequiredId(request.SessionQuestionId, nameof(request.SessionQuestionId));
        var examId = await store.FindExamIdBySessionAsync(request.SessionId, ct);
        await guard.CanManageExamAsync(examId, ct);
        return await store.SaveTeacherReviewAsync(request, ct);
    }

    public async Task<VivaSessionGradingSheet> ApproveAllAiScoresAsync(Guid sessionId, string? comment, CancellationToken ct)
    {
        DomainRules.RequiredId(sessionId, nameof(sessionId));
        var examId = await store.FindExamIdBySessionAsync(sessionId, ct);
        await guard.CanManageExamAsync(examId, ct);
        return await store.ApproveAllAiScoresAsync(sessionId, comment, ct);
    }

    public async Task<StudentSessionGradingResult?> GetStudentResultAsync(Guid sessionId, CancellationToken ct)
    {
        if (actor.Id == Guid.Empty || actor.IsAdministrator || actor.IsLecturer)
            throw new UnauthorizedAccessException("Student access is required.");

        DomainRules.RequiredId(sessionId, nameof(sessionId));
        var enrollmentId = await sessionStore.FindEnrollmentAsync(sessionId, ct);
        await guard.OwnExamStudentAsync(enrollmentId, ct);
        return await store.GetStudentResultAsync(sessionId, ct);
    }

    public static (int TotalSeconds, int TotalWords, int WordsPerMinute, string FluencyNote) CalculateFluencySignals(
        IEnumerable<(DateTimeOffset AskedAt, DateTimeOffset? AnsweredAt, int? AudioDurationSeconds, string? Transcript)> answeredTurns)
    {
        var list = answeredTurns.Where(t => t.AnsweredAt.HasValue && !string.IsNullOrWhiteSpace(t.Transcript)).ToArray();
        if (list.Length == 0)
        {
            return (0, 0, 0, "Chưa có câu trả lời được ghi nhận.");
        }

        var totalSeconds = 0;
        var totalWords = 0;
        foreach (var turn in list)
        {
            var sec = turn.AudioDurationSeconds is > 0
                ? turn.AudioDurationSeconds.Value
                : Math.Max(1, (int)Math.Round((turn.AnsweredAt!.Value - turn.AskedAt).TotalSeconds));
            totalSeconds += sec;

            var words = System.Text.RegularExpressions.Regex.Matches(turn.Transcript!, @"[\p{L}\p{N}]+").Count;
            totalWords += words;
        }

        var wpm = totalSeconds > 0 ? (int)Math.Round(totalWords * 60.0 / totalSeconds) : 0;
        var note = wpm switch
        {
            0 => "Chưa đủ dữ liệu âm thanh/thời gian để tính độ trôi chảy.",
            < 50 => $"Phản hồi khá chậm hoặc ngắt quãng (~{wpm} từ/phút, tổng {totalSeconds}s qua {list.Length} lượt trả lời).",
            <= 160 => $"Trình bày mạch lạc, tốc độ ổn định (~{wpm} từ/phút, tổng {totalSeconds}s qua {list.Length} lượt trả lời).",
            _ => $"Tốc độ trình bày nhanh (~{wpm} từ/phút, tổng {totalSeconds}s qua {list.Length} lượt trả lời)."
        };

        return (totalSeconds, totalWords, wpm, note);
    }
}
