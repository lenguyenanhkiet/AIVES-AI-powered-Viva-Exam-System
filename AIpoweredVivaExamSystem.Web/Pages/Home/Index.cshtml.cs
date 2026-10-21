using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics;
using AIpoweredVivaExamSystem.Application.Questions;
using AIpoweredVivaExamSystem.Application.Questions.DTOs;
using AIpoweredVivaExamSystem.Application.Rubrics;
using AIpoweredVivaExamSystem.Application.Rubrics.DTOs;
using AIpoweredVivaExamSystem.Application.Subjects;
using AIpoweredVivaExamSystem.Application.Subjects.DTOs;
using AIpoweredVivaExamSystem.Application.Topics;
using AIpoweredVivaExamSystem.Application.Topics.DTOs;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

using AIpoweredVivaExamSystem.Web.Common;

namespace AIpoweredVivaExamSystem.Web.Pages.Home;


public sealed class IndexModel(ISubjectService subjects, ITopicService topics, IQuestionService questions, IRubricService rubrics) : PageModel
{
    public DashboardViewModel Vm { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var recent = await subjects.ListAsync(new SubjectListQuery { PageSize = 5 }, cancellationToken);
        var topicPage = await topics.ListAsync(new TopicListQuery { PageSize = 1 }, cancellationToken);
        var questionPage = await questions.ListAsync(new QuestionListQuery { PageSize = 1 }, cancellationToken);
        var draftPage = await questions.ListAsync(new QuestionListQuery { Status = QuestionStatus.Draft, PageSize = 5 }, cancellationToken);
        var approvedPage = await questions.ListAsync(new QuestionListQuery { Status = QuestionStatus.Approved, PageSize = 1 }, cancellationToken);
        var rubricPage = await rubrics.ListAsync(new RubricListQuery { PageSize = 1 }, cancellationToken);
        Vm = new DashboardViewModel
        {
            SubjectCount = recent.TotalCount,
            TopicCount = topicPage.TotalCount,
            QuestionCount = questionPage.TotalCount,
            DraftCount = draftPage.TotalCount,
            ApprovedCount = approvedPage.TotalCount,
            RubricCount = rubricPage.TotalCount,
            RecentSubjects = recent.Items,
            PendingQuestions = draftPage.Items
        };
        return Page();
    }

    public IActionResult OnPost() => NotFound();
}
