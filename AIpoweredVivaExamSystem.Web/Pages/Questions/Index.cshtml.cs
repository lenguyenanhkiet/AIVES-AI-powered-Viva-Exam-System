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
public sealed class IndexModel(IQuestionService questions, ISubjectService subjects, ITopicService topics) : ContentPageModel
{
    public async Task<IActionResult> OnGetAsync([FromQuery] QuestionListQuery query, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || query.SubjectId == Guid.Empty || query.TopicId == Guid.Empty
            || query.Page < 1 || query.PageSize is < 1 or > 100
            || (query.BloomLevel is { } bloom && !Enum.IsDefined(bloom))
            || (query.Difficulty is { } difficulty && !Enum.IsDefined(difficulty))
            || (query.Status is { } status && !Enum.IsDefined(status)))
            query = new QuestionListQuery();

        var page = await questions.ListAsync(query, cancellationToken);
        var counts = new Dictionary<QuestionStatus, int>();
        foreach (var tab in StatusTabs)
            counts[tab] = (await questions.ListAsync(CountQuery(query, tab), cancellationToken)).TotalCount;
        var all = (await questions.ListAsync(CountQuery(query, null), cancellationToken)).TotalCount;

        return RenderContent("/Pages/Questions/_IndexContent.cshtml", new QuestionIndexViewModel
        {
            Page = page, Query = query, StatusCounts = counts, AllCount = all,
            Subjects = await SubjectMapAsync(cancellationToken),
            Topics = await TopicMapAsync(query.SubjectId, cancellationToken)
        });
    }

    private static readonly QuestionStatus[] StatusTabs =
        [QuestionStatus.Draft, QuestionStatus.Approved, QuestionStatus.Rejected];

    private static QuestionListQuery CountQuery(QuestionListQuery query, QuestionStatus? status) => new()
    {
        SubjectId = query.SubjectId, TopicId = query.TopicId, Keyword = query.Keyword,
        BloomLevel = query.BloomLevel, Difficulty = query.Difficulty, Status = status, PageSize = 1
    };

    private async Task<IReadOnlyDictionary<Guid, SubjectResponse>> SubjectMapAsync(CancellationToken cancellationToken) =>
        (await subjects.ListAsync(new SubjectListQuery { PageSize = 100 }, cancellationToken)).Items.ToDictionary(x => x.Id);

    private async Task<IReadOnlyDictionary<Guid, TopicResponse>> TopicMapAsync(Guid? subjectId, CancellationToken cancellationToken) =>
        (await topics.ListAsync(new TopicListQuery { SubjectId = subjectId, PageSize = 100 }, cancellationToken)).Items.ToDictionary(x => x.Id);

    public IActionResult OnPost() => NotFound();
}
