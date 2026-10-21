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
public sealed class IndexModel(IExamRosterReader details) : ContentPageModel
{
    public async Task<IActionResult> OnGetAsync([FromQuery] int page, CancellationToken ct) =>
        RenderContent("/Pages/ExamStudents/_IndexContent.cshtml", await details.ListManagedAsync(ModelState.IsValid ? page : 1, ct));

    public IActionResult OnPost() => NotFound();
}
