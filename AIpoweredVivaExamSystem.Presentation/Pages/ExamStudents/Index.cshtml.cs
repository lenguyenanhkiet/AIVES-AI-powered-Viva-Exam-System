using AIpoweredVivaExamSystem.Business.ExamStudents;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Presentation.Common;
using AIpoweredVivaExamSystem.Presentation.ViewModels.ExamStudents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace AIpoweredVivaExamSystem.Presentation.Pages.ExamStudents;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class IndexModel(ExamRosterService roster) : PageModel
{
    public AIpoweredVivaExamSystem.DataAccess.Models.ExamStudents.RosterExamPage Vm { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync([FromQuery] int page, CancellationToken ct)
    {
        Vm = await roster.ListManagedAsync(ModelState.IsValid ? page : 1, ct);
        return Page();
    }

    public IActionResult OnPost() => NotFound();
}
