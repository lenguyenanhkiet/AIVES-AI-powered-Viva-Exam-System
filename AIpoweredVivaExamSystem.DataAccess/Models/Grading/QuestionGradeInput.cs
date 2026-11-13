using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Grading;

public sealed class QuestionGradeInput
{
    public Guid SessionQuestionId { get; set; }
    public string? Comment { get; set; }
    public List<CriterionGradeInput> Criteria { get; set; } = [];
}
