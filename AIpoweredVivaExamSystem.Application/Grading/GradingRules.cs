using System.Text.Json;
using AIpoweredVivaExamSystem.Application.Common;

namespace AIpoweredVivaExamSystem.Application.Grading;

public static class GradingRules
{
    public const string UnavailableSuggestionMessage="AI không phản hồi hoặc trả kết quả không hợp lệ.";
    public static QuestionSuggestion NoAnswerSuggestion(IReadOnlyList<GradingCriterion> criteria) =>
        new(criteria.Select(c=>new SuggestedCriterion(c.Id,0,"Không ghi nhận được câu trả lời cho tiêu chí này.")).ToArray(),
            "", "Không ghi nhận được câu trả lời.", "Chưa thể hiện khái niệm nào của rubric.", "server-no-answer");

    public static (IReadOnlyList<GradingCriterion> Criteria, string? ExpectedAnswer) ReadRubric(string snapshot)
    {
        using var json = JsonDocument.Parse(snapshot);
        var root = json.RootElement;
        var criteria = root.GetProperty("Criteria").EnumerateArray().Select(c => new GradingCriterion(
            c.GetProperty("Id").GetGuid(), c.GetProperty("Name").GetString()!, Optional(c,"Description"),
            Optional(c,"ExpectedConcepts"), c.GetProperty("MaxScore").GetDecimal(), c.GetProperty("DisplayOrder").GetInt32()))
            .OrderBy(c => c.DisplayOrder).ToArray();
        if (criteria.Length == 0 || criteria.Any(c => c.Id == Guid.Empty || c.MaxScore <= 0) ||
            criteria.Select(c => c.Id).Distinct().Count() != criteria.Length)
            throw new ResourceConflictException("Rubric đã lưu không hợp lệ.");
        return (criteria, Optional(root,"ExpectedAnswer"));
    }
    private static string? Optional(JsonElement node,string name) =>
        node.TryGetProperty(name,out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    public static QuestionSuggestion ValidateSuggestion(QuestionGradingContext context, QuestionSuggestion suggestion)
    {
        if (suggestion.Criteria.Count != context.Criteria.Count || suggestion.Criteria.Select(c => c.CriterionId).Distinct().Count() != context.Criteria.Count ||
            suggestion.Criteria.Any(c => context.Criteria.All(r => r.Id != c.CriterionId)) ||
            string.IsNullOrWhiteSpace(suggestion.ModelName) || suggestion.ModelName.Length > 100 ||
            suggestion.Criteria.Any(c => string.IsNullOrWhiteSpace(c.Justification) || c.Justification.Length > 2000) ||
            new[] { suggestion.Strengths, suggestion.Weaknesses, suggestion.MissingPoints }.Any(s => s is null || s.Length > 4000))
            throw new ResourceConflictException("AI trả về gợi ý chấm điểm không hợp lệ.");
        var noAnswer = context.Conversation.All(t => string.IsNullOrWhiteSpace(t.Transcript));
        return suggestion with { Criteria = context.Criteria.Select(c => {
            var score = suggestion.Criteria.Single(s => s.CriterionId == c.Id);
            return score with { Score = noAnswer ? 0 : decimal.Round(Math.Clamp(score.Score,0,c.MaxScore),2) };
        }).ToArray() };
    }
    public static bool IsNoAnswerMarker(string? text) => text is
        "[Hết thời gian: không có nội dung được ghi nhận; xem bản ghi âm nếu có.]" or
        "[Timed out: no transcript captured; see attached audio if available.]";
}
