using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Questions;
using AIpoweredVivaExamSystem.Application.Questions.DTOs;
using AIpoweredVivaExamSystem.Application.Subjects;
using AIpoweredVivaExamSystem.Application.Subjects.DTOs;
using AIpoweredVivaExamSystem.Application.Topics;
using AIpoweredVivaExamSystem.Application.Topics.DTOs;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Web.Common;
using AIpoweredVivaExamSystem.Web.ViewModels;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;


namespace AIpoweredVivaExamSystem.Web.Pages.Subjects;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,LECTURER")]
public sealed class DetailsModel(ISubjectService subjects, ITopicService topics, IQuestionService questions) : ContentPageModel
{
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var subject = await subjects.GetAsync(id, cancellationToken);
        var subjectTopics = await topics.ListAsync(new TopicListQuery { SubjectId = id, PageSize = 100 }, cancellationToken);
        var questionCount = (await questions.ListAsync(new QuestionListQuery { SubjectId = id, PageSize = 1 }, cancellationToken)).TotalCount;
        return RenderContent("/Pages/Subjects/_DetailsContent.cshtml", new SubjectDetailsViewModel { Subject = subject, Topics = subjectTopics, QuestionCount = questionCount });
    }

    public IActionResult OnPost() => NotFound();
}
