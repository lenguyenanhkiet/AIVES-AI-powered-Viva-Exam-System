using AIpoweredVivaExamSystem.Business.VivaSessions;
using AIpoweredVivaExamSystem.Business.Topics;
using AIpoweredVivaExamSystem.Business.Subjects;
using AIpoweredVivaExamSystem.Business.Rubrics;
using AIpoweredVivaExamSystem.Business.Questions;
using AIpoweredVivaExamSystem.DataAccess.Entities;
using AIpoweredVivaExamSystem.DataAccess.Enums;
using AIpoweredVivaExamSystem.DataAccess.Models.Questions;
using AIpoweredVivaExamSystem.DataAccess.Models.Rubrics;
using AIpoweredVivaExamSystem.DataAccess.Models.Subjects;
using AIpoweredVivaExamSystem.DataAccess.Models.Topics;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics;
using AIpoweredVivaExamSystem.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

using AIpoweredVivaExamSystem.Presentation.Common;

namespace AIpoweredVivaExamSystem.Presentation.Pages.Home;


public sealed class IndexModel(MyVivaSessionsService mySessions, SubjectService subjects, TopicService topics, QuestionService questions, RubricService rubrics) : PageModel
{
    public DashboardViewModel Vm { get; private set; } = default!;

    // Students get their own overview: the question bank is staff-only.
    public IReadOnlyList<AIpoweredVivaExamSystem.DataAccess.Models.VivaSessions.MyVivaExam>? MyExams { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (User.IsInRole("STUDENT"))
        {
            MyExams = await mySessions.ListAsync(cancellationToken);
            return Page();
        }

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
