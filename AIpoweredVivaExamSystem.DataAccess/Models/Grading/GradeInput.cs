using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Grading;

public sealed class GradeInput
{
    public string Version { get; set; } = "";
    public string? OverallComment { get; set; }
    public List<QuestionGradeInput> Questions { get; set; } = [];
}
