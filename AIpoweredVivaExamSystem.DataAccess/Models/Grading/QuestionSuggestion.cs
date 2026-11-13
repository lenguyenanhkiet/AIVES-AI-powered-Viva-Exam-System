using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Grading;

public sealed record QuestionSuggestion(IReadOnlyList<SuggestedCriterion> Criteria, string Strengths, string Weaknesses,
    string MissingPoints, string ModelName);
