using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.Business.Grading;

public interface IAiRubricGrader
{ Task<QuestionSuggestion> SuggestAsync(QuestionGradingContext context, CancellationToken ct); }
