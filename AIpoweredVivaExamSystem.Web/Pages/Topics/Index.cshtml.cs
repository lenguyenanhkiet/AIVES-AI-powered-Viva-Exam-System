using Microsoft.AspNetCore.Mvc.RazorPages;
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


namespace AIpoweredVivaExamSystem.Web.Pages.Topics;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,LECTURER")]
public sealed class IndexModel(ITopicService topics, ISubjectService subjects) : PageModel
{
    public TopicIndexViewModel Vm { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync([FromQuery] TopicListQuery query, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || query.SubjectId == Guid.Empty || query.Page < 1 || query.PageSize is < 1 or > 100
            || (query.Status is { } status && !Enum.IsDefined(status)))
            query = new TopicListQuery();
        var page = await topics.ListAsync(query, cancellationToken);
        Vm = new TopicIndexViewModel
        {
            Page = page, Query = query, Subjects = await SubjectMapAsync(cancellationToken)
        };
        return Page();
    }

    private async Task<IReadOnlyDictionary<Guid, SubjectResponse>> SubjectMapAsync(CancellationToken cancellationToken) =>
        (await subjects.ListAsync(new SubjectListQuery { PageSize = 100 }, cancellationToken)).Items.ToDictionary(x => x.Id);

    public IActionResult OnPost() => NotFound();
}
