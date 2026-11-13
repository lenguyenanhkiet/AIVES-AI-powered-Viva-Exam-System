using AIpoweredVivaExamSystem.Business.Topics;
using AIpoweredVivaExamSystem.Business.Subjects;
using AIpoweredVivaExamSystem.Business.Questions;
using AIpoweredVivaExamSystem.DataAccess.Entities;
using AIpoweredVivaExamSystem.DataAccess.Models.Questions;
using AIpoweredVivaExamSystem.DataAccess.Models.Topics;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Presentation.Common;
using AIpoweredVivaExamSystem.Presentation.ViewModels;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;


namespace AIpoweredVivaExamSystem.Presentation.Pages.Subjects;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,LECTURER")]
public sealed class DetailsModel(SubjectService subjects, TopicService topics, QuestionService questions) : PageModel
{
    public SubjectDetailsViewModel Vm { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var subject = await subjects.GetAsync(id, cancellationToken);
        var subjectTopics = await topics.ListAsync(new TopicListQuery { SubjectId = id, PageSize = 100 }, cancellationToken);
        var questionCount = (await questions.ListAsync(new QuestionListQuery { SubjectId = id, PageSize = 1 }, cancellationToken)).TotalCount;
        Vm = new SubjectDetailsViewModel { Subject = subject, Topics = subjectTopics, QuestionCount = questionCount };
        return Page();
    }

    public IActionResult OnPost() => NotFound();
}
