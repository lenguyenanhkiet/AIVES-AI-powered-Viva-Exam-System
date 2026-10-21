using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Web.Common;
using AIpoweredVivaExamSystem.Web.ViewModels.ExamPools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace AIpoweredVivaExamSystem.Web.Pages.ExamPools;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class RemoveModel(ExamPoolService pools) : PageModel
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
            await pools.RemoveAsync(id, Input.QuestionIds, ct);
            TempData.Toast("Đã bỏ câu hỏi khỏi pool.");
            return RedirectToPage("/ExamPools/Index", new { id });
        }
        catch (ResourceConflictException ex)
        {
            TempData.Toast(ex.Message, success: false);
            return RedirectToPage("/ExamPools/Index", new { id });
        }
        catch (DomainValidationException ex)
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
