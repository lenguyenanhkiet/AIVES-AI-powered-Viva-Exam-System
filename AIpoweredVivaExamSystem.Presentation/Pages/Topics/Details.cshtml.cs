using AIpoweredVivaExamSystem.Business.Topics;
using AIpoweredVivaExamSystem.Business.Subjects;
using AIpoweredVivaExamSystem.Business.Questions;
using AIpoweredVivaExamSystem.DataAccess.Entities;
using AIpoweredVivaExamSystem.DataAccess.Models.Questions;
using AIpoweredVivaExamSystem.DataAccess.Models.Subjects;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Presentation.Common;
using AIpoweredVivaExamSystem.Presentation.ViewModels;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;


namespace AIpoweredVivaExamSystem.Presentation.Pages.Topics;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,LECTURER")]
public sealed class DetailsModel(TopicService topics, QuestionService questions, SubjectService subjects) : PageModel
{
    public TopicDetailsViewModel Vm { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var topic = await topics.GetAsync(id, cancellationToken);
        (await SubjectMapAsync(cancellationToken)).TryGetValue(topic.SubjectId, out var subject);
        var topicQuestions = await questions.ListAsync(new QuestionListQuery { TopicId = id, PageSize = 10 }, cancellationToken);
        Vm = new TopicDetailsViewModel { Topic = topic, Subject = subject, Questions = topicQuestions };
        return Page();
    }

    private async Task<IReadOnlyDictionary<Guid, SubjectResponse>> SubjectMapAsync(CancellationToken cancellationToken) =>
        (await subjects.ListAsync(new SubjectListQuery { PageSize = 100 }, cancellationToken)).Items.ToDictionary(x => x.Id);

    public IActionResult OnPost() => NotFound();
}
