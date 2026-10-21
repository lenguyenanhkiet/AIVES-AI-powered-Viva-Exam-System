using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Application.ExamStudents;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Web.Common;
using AIpoweredVivaExamSystem.Web.ViewModels.ExamStudents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace AIpoweredVivaExamSystem.Web.Pages.ExamStudents;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class IndexModel(IExamRosterReader details) : PageModel
{
    public AIpoweredVivaExamSystem.Application.ExamStudents.RosterExamPage Vm { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync([FromQuery] int page, CancellationToken ct)
    {
        Vm = await details.ListManagedAsync(ModelState.IsValid ? page : 1, ct);
        return Page();
    }

    public IActionResult OnPost() => NotFound();
}
