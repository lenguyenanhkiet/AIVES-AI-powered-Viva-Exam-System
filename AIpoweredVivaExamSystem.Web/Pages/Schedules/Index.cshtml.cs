using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Application.Scheduling;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Web.Common;
using AIpoweredVivaExamSystem.Web.ViewModels.Schedules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ExamEntity = AIpoweredVivaExamSystem.Domain.Entities.Exam;


namespace AIpoweredVivaExamSystem.Web.Pages.Schedules;

[Authorize]
[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class IndexModel(IScheduleReader reader) : ContentPageModel
{
    public async Task<IActionResult> OnGetAsync(CancellationToken ct) =>
        RenderContent("/Pages/Schedules/_IndexContent.cshtml", new ScheduleIndexViewModel { Exams = await reader.ListManagedAsync(ct) });

    public IActionResult OnPost() => NotFound();
}
