using AIpoweredVivaExamSystem.DataAccess.Entities;
using AIpoweredVivaExamSystem.DataAccess.Models.Exams;
using System.ComponentModel.DataAnnotations;
namespace AIpoweredVivaExamSystem.Presentation.ViewModels.ExamPools;

public sealed class ExamPoolSelectionViewModel
{
    [MinLength(1, ErrorMessage = "Hãy chọn ít nhất một câu hỏi.")]
    public List<Guid> QuestionIds { get; set; } = [];
}

public sealed class ExamPoolViewModel
{
    public required ExamPoolPage Page { get; init; }
    public ExamPoolSelectionViewModel Form { get; init; } = new();
    public bool CanEdit => Page.Exam.Status == ExamStatus.Draft;
}
