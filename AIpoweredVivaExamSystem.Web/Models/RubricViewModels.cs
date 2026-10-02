using AIpoweredVivaExamSystem.Application.Questions.DTOs;
using AIpoweredVivaExamSystem.Application.Rubrics.DTOs;

namespace AIpoweredVivaExamSystem.Web.Models;

public sealed class RubricDetailsViewModel
{
    public required QuestionResponse Question { get; init; }
    public RubricResponse? Rubric { get; init; }
}
