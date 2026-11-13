using AIpoweredVivaExamSystem.Business.Scheduling;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Presentation.Common;
using AIpoweredVivaExamSystem.Presentation.ViewModels.Schedules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ExamEntity = AIpoweredVivaExamSystem.DataAccess.Entities.Exam;


namespace AIpoweredVivaExamSystem.Presentation.Pages.Schedules;

[Authorize]
[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class IndexModel(ExamScheduleService schedules) : PageModel
{
    public AIpoweredVivaExamSystem.Presentation.ViewModels.Schedules.ScheduleIndexViewModel Vm { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Vm = new ScheduleIndexViewModel { Exams = await schedules.ListManagedAsync(ct) };
        return Page();
    }

    public IActionResult OnPost() => NotFound();
}
