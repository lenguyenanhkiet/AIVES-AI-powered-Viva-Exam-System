using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Grading;

public sealed record CriterionReview(Guid Id, string Name, string? Description, string? ExpectedConcepts, decimal MaxScore,
    int DisplayOrder, decimal? SuggestedScore, string? Justification, decimal? LecturerScore);
