using AIpoweredVivaExamSystem.Business.Topics;
using AIpoweredVivaExamSystem.Business.Subjects;
using AIpoweredVivaExamSystem.Business.Rubrics;
using AIpoweredVivaExamSystem.Business.Questions;
using AIpoweredVivaExamSystem.Business.QuestionBank;
using AIpoweredVivaExamSystem.DataAccess.Entities;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Presentation.Common;
using AIpoweredVivaExamSystem.Presentation.ViewModels;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;


namespace AIpoweredVivaExamSystem.Presentation.Pages.Questions;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,LECTURER")]
public sealed class DetailsModel(QuestionService questions, RubricService rubrics, SubjectService subjects, TopicService topics, QuestionBankReadService questionBank) : PageModel
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
            Citations = await questionBank.CitationsAsync(id, cancellationToken)
        };
        return Page();
    }

    public IActionResult OnPost() => NotFound();
}
