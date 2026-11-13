using AIpoweredVivaExamSystem.Business.Exams;
using AIpoweredVivaExamSystem.DataAccess.Common;
using AIpoweredVivaExamSystem.DataAccess.Models.Common;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Presentation.Common;
using AIpoweredVivaExamSystem.Presentation.ViewModels.ExamPools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace AIpoweredVivaExamSystem.Presentation.Pages.ExamPools;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class AddModel(ExamPoolService pools) : PageModel
{
    public ExamPoolViewModel Vm { get; private set; } = default!;

    [BindProperty]
    public ExamPoolSelectionViewModel Input { get; set; } = new();

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
    {
        var page = await PageAsync(id, Input, ct);
        if (!ModelState.IsValid)
        {
            Vm = page;
            return Page();
        }
        try
        {
            await pools.AddAsync(id, Input.QuestionIds, ct);
            TempData.Toast("Đã thêm câu hỏi vào pool.");
            return RedirectToPage("/ExamPools/Index", new { id });
        }
        catch (Exception ex) when (ex is DomainValidationException or ResourceConflictException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            Vm = await PageAsync(id, Input, ct);
            return Page();
        }
    }

    private async Task<ExamPoolViewModel> PageAsync(Guid examId, ExamPoolSelectionViewModel form, CancellationToken ct) =>
        new() { Page = await pools.GetPageAsync(examId, ct), Form = form };

    public IActionResult OnGet() => NotFound();
}
