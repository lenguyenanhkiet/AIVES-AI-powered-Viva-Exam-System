using System.Text.Json;
using System.Text.RegularExpressions;
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

    public async Task<VivaSessionGradingSheet> RunQuestionAiGradingAsync(Guid sessionId, Guid sessionQuestionId, CancellationToken ct)
    {
        DomainRules.RequiredId(sessionId, nameof(sessionId));
        DomainRules.RequiredId(sessionQuestionId, nameof(sessionQuestionId));
        var examId = await store.FindExamIdBySessionAsync(sessionId, ct);
        await guard.CanManageExamAsync(examId, ct);
        return await store.GenerateQuestionAiGradingAsync(sessionId, sessionQuestionId, ct);
    }

    public async Task<int> RunExamBatchAiGradingAsync(Guid examId, CancellationToken ct)
    {
        DomainRules.RequiredId(examId, nameof(examId));
        await guard.CanManageExamAsync(examId, ct);
        return await store.GenerateExamAiGradingsAsync(examId, ct);
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

            var words = Regex.Matches(turn.Transcript!, @"[\p{L}\p{N}]+").Count;
            totalWords += words;
        }

        var wpm = totalSeconds > 0 ? (int)Math.Round(totalWords * 60.0 / totalSeconds) : 0;
        var note = FormatFluencyNote(totalSeconds, totalWords, wpm, list.Length);
        return (totalSeconds, totalWords, wpm, note);
    }

    public static string FormatFluencyNote(int totalSeconds, int totalWords, int wordsPerMinute, int turnCount)
    {
        if (turnCount <= 0 || totalWords <= 0)
            return "Chưa có câu trả lời được ghi nhận.";
        if (totalSeconds <= 0 || wordsPerMinute <= 0)
            return $"Tổng số từ: {totalWords} từ qua {turnCount} lượt trả lời.";

        return wordsPerMinute switch
        {
            < 50 => $"Phản hồi khá chậm hoặc ngắt quãng (~{wordsPerMinute} từ/phút, {totalWords} từ trong {totalSeconds}s qua {turnCount} lượt trả lời).",
            <= 160 => $"Trình bày mạch lạc, tốc độ ổn định (~{wordsPerMinute} từ/phút, {totalWords} từ trong {totalSeconds}s qua {turnCount} lượt trả lời).",
            _ => $"Tốc độ trình bày nhanh (~{wordsPerMinute} từ/phút, {totalWords} từ trong {totalSeconds}s qua {turnCount} lượt trả lời)."
        };
    }

    public static (string RubricName, string? ExpectedAnswer, decimal MaxScore, IReadOnlyList<RubricCriterionSnapshotItem> Criteria) ParseRubricSnapshot(
        string rubricSnapshotJson,
        decimal fallbackMaxScore = 10m)
    {
        var defaultMax = fallbackMaxScore > 0 ? fallbackMaxScore : 10m;
        var rubricName = "Rubric chấm điểm";
        string? expectedAnswer = null;
        decimal totalScore = defaultMax;
        var criteriaList = new List<RubricCriterionSnapshotItem>();

        try
        {
            using var doc = JsonDocument.Parse(rubricSnapshotJson);
            var root = doc.RootElement;
            rubricName = OptionalJsonString(root, "Name") ?? OptionalJsonString(root, "Title") ?? rubricName;
            expectedAnswer = OptionalJsonString(root, "ExpectedAnswer");

            if (root.TryGetProperty("TotalScore", out var tsProp) && tsProp.TryGetDecimal(out var tsVal) && tsVal > 0)
            {
                totalScore = decimal.Round(tsVal, 2, MidpointRounding.AwayFromZero);
            }

            if (root.TryGetProperty("Criteria", out var cArr) && cArr.ValueKind == JsonValueKind.Array)
            {
                var idx = 1;
                foreach (var item in cArr.EnumerateArray())
                {
                    Guid? id = null;
                    if (item.TryGetProperty("Id", out var idProp) && idProp.ValueKind == JsonValueKind.String &&
                        Guid.TryParse(idProp.GetString(), out var parsedId) && parsedId != Guid.Empty)
                    {
                        id = parsedId;
                    }

                    var name = OptionalJsonString(item, "Name") ?? $"Tiêu chí #{idx}";
                    var desc = OptionalJsonString(item, "Description");
                    var concepts = OptionalJsonString(item, "ExpectedConcepts");
                    var max = item.TryGetProperty("MaxScore", out var mProp) && mProp.TryGetDecimal(out var mVal) && mVal > 0
                        ? decimal.Round(mVal, 2, MidpointRounding.AwayFromZero)
                        : 0m;
                    var order = item.TryGetProperty("DisplayOrder", out var oProp) && oProp.TryGetInt32(out var oVal) && oVal > 0
                        ? oVal
                        : idx;

                    criteriaList.Add(new RubricCriterionSnapshotItem(id, name, desc, concepts, max, order));
                    idx++;
                }
            }
        }
        catch (JsonException)
        {
            // Fallback to single generic criterion below.
        }

        if (criteriaList.Count == 0)
        {
            criteriaList.Add(new RubricCriterionSnapshotItem(
                null,
                "Nội dung & mức độ hiểu bài",
                "Đánh giá mức độ chính xác và đầy đủ của câu trả lời",
                expectedAnswer,
                totalScore,
                1));
        }
        else
        {
            var sumCriteria = criteriaList.Sum(c => c.MaxScore);
            if (sumCriteria <= 0)
            {
                var each = decimal.Round(totalScore / criteriaList.Count, 2, MidpointRounding.AwayFromZero);
                for (var i = 0; i < criteriaList.Count; i++)
                {
                    var m = i == criteriaList.Count - 1 ? Math.Max(0.01m, totalScore - each * (criteriaList.Count - 1)) : each;
                    criteriaList[i] = criteriaList[i] with { MaxScore = m };
                }
            }
            else
            {
                totalScore = sumCriteria;
            }
        }

        return (rubricName, expectedAnswer, totalScore, criteriaList);
    }

    private static string? OptionalJsonString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.String)
            return null;
        var value = prop.GetString()?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
