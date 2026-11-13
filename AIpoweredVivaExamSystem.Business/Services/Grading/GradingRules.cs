using AIpoweredVivaExamSystem.DataAccess.Models.Grading;
namespace AIpoweredVivaExamSystem.Business.Grading;

public static class GradingRules
{
    public const string UnavailableSuggestionMessage = GradingSnapshot.UnavailableSuggestionMessage;
    public static QuestionSuggestion NoAnswerSuggestion(IReadOnlyList<GradingCriterion> criteria) => GradingSnapshot.NoAnswerSuggestion(criteria);
    public static (IReadOnlyList<GradingCriterion> Criteria, string? ExpectedAnswer) ReadRubric(string snapshot) => GradingSnapshot.ReadRubric(snapshot);
    public static bool IsNoAnswerMarker(string? text) => GradingSnapshot.IsNoAnswerMarker(text);
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
}
