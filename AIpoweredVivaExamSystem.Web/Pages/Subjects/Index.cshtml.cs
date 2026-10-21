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
public sealed class IndexModel(ISubjectService subjects) : ContentPageModel
{
    public async Task<IActionResult> OnGetAsync([FromQuery] SubjectListQuery query, CancellationToken cancellationToken)
    {
        // Invalid filters in a hand-edited URL fall back to the default list instead of an error page.
        if (!ModelState.IsValid || query.Page < 1 || query.PageSize is < 1 or > 100
            || (query.Status is { } status && !Enum.IsDefined(status)))
            query = new SubjectListQuery();
        var page = await subjects.ListAsync(query, cancellationToken);
        return RenderContent("/Pages/Subjects/_IndexContent.cshtml", new SubjectIndexViewModel { Page = page, Query = query });
    }

    public IActionResult OnPost() => NotFound();
}
