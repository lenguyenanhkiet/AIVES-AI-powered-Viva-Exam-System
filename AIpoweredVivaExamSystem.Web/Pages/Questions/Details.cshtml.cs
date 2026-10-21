using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Questions;
using AIpoweredVivaExamSystem.Application.Questions.DTOs;
using AIpoweredVivaExamSystem.Application.Rubrics;
using AIpoweredVivaExamSystem.Application.Subjects;
using AIpoweredVivaExamSystem.Application.Subjects.DTOs;
using AIpoweredVivaExamSystem.Application.Topics;
using AIpoweredVivaExamSystem.Application.Topics.DTOs;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Web.Common;
using AIpoweredVivaExamSystem.Web.ViewModels;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;


namespace AIpoweredVivaExamSystem.Web.Pages.Questions;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,LECTURER")]
public sealed class DetailsModel(IQuestionService questions, ISubjectService subjects, ITopicService topics, IRubricService rubrics, AIpoweredVivaExamSystem.Application.QuestionBank.IQuestionBankStore bank) : PageModel
{
    public QuestionDetailsViewModel Vm { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var question = await questions.GetAsync(id, cancellationToken);
        var rubric = question.HasRubric
            ? await rubrics.GetByQuestionIdAsync(id, cancellationToken)
            : null;
        Vm = new QuestionDetailsViewModel
        {
            Question = question,
            Subject = await subjects.GetAsync(question.SubjectId, cancellationToken),
            Topic = question.TopicId is { } topicId ? await topics.GetAsync(topicId, cancellationToken) : null,
            Rubric = rubric,
            Citations = await bank.CitationsAsync(id, cancellationToken)
        };
        return Page();
    }

    public IActionResult OnPost() => NotFound();
}
