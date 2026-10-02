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
using AIpoweredVivaExamSystem.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace AIpoweredVivaExamSystem.Web.Controllers;

public sealed class HomeController(
    ISubjectService subjects, ITopicService topics, IQuestionService questions, IRubricService rubrics) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var recent = await subjects.ListAsync(new SubjectListQuery { PageSize = 5 }, cancellationToken);
        var topicPage = await topics.ListAsync(new TopicListQuery { PageSize = 1 }, cancellationToken);
        var questionPage = await questions.ListAsync(new QuestionListQuery { PageSize = 1 }, cancellationToken);
        var draftPage = await questions.ListAsync(new QuestionListQuery { Status = QuestionStatus.Draft, PageSize = 5 }, cancellationToken);
        var approvedPage = await questions.ListAsync(new QuestionListQuery { Status = QuestionStatus.Approved, PageSize = 1 }, cancellationToken);
        var rubricPage = await rubrics.ListAsync(new RubricListQuery { PageSize = 1 }, cancellationToken);
        return View(new DashboardViewModel
        {
            SubjectCount = recent.TotalCount,
            TopicCount = topicPage.TotalCount,
            QuestionCount = questionPage.TotalCount,
            DraftCount = draftPage.TotalCount,
            ApprovedCount = approvedPage.TotalCount,
            RubricCount = rubricPage.TotalCount,
            RecentSubjects = recent.Items,
            PendingQuestions = draftPage.Items
        });
    }

    [AllowAnonymous, Route("Home/StatusCode/{code:int}")]
    public new IActionResult StatusCode(int code)
    {
        Response.StatusCode = code;
        return View("StatusCode", code);
    }

    [AllowAnonymous, ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
