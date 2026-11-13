using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Grading;

public sealed record QuestionGradingContext(string Question, string? ExpectedAnswer, IReadOnlyList<GradingCriterion> Criteria,
    IReadOnlyList<GradeConversation> Conversation);
