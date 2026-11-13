using AIpoweredVivaExamSystem.Business.Subjects;
using AIpoweredVivaExamSystem.DataAccess.Models.Subjects;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Presentation.Common;
using AIpoweredVivaExamSystem.Presentation.ViewModels;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;


namespace AIpoweredVivaExamSystem.Presentation.Pages.Subjects;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,LECTURER")]
public sealed class IndexModel(SubjectService subjects) : PageModel
{
    public SubjectIndexViewModel Vm { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync([FromQuery] SubjectListQuery query, CancellationToken cancellationToken)
    {
        // Invalid filters in a hand-edited URL fall back to the default list instead of an error page.
        if (!ModelState.IsValid || query.Page < 1 || query.PageSize is < 1 or > 100
            || (query.Status is { } status && !Enum.IsDefined(status)))
            query = new SubjectListQuery();
        var page = await subjects.ListAsync(query, cancellationToken);
        Vm = new SubjectIndexViewModel { Page = page, Query = query };
        return Page();
    }

    public IActionResult OnPost() => NotFound();
}
