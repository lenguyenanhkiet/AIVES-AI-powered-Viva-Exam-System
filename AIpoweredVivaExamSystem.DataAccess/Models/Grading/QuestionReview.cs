using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Grading;

public sealed record QuestionReview(Guid SessionQuestionId, int Position, string QuestionText, string? ExpectedAnswer,
    IReadOnlyList<CriterionReview> Criteria, IReadOnlyList<TurnEvidence> Turns, EvidenceSignals Signals,
    string SuggestionState, string? SuggestionError, string? AiModel, DateTimeOffset? SuggestedAt,
    string? Strengths, string? Weaknesses, string? MissingPoints, decimal? SuggestedTotal, decimal? LecturerTotal,
    decimal MaxScore, string? LecturerComment);
