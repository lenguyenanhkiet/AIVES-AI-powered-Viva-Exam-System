using AIpoweredVivaExamSystem.Business.Questions;
using AIpoweredVivaExamSystem.DataAccess.Models.Questions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Presentation.Common;
using AIpoweredVivaExamSystem.Presentation.ViewModels;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;


namespace AIpoweredVivaExamSystem.Presentation.Pages.Questions;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,LECTURER")]
public sealed class DeleteModel(QuestionService questions) : PageModel
{
    public QuestionResponse Vm { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        Vm = await questions.GetAsync(id, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken cancellationToken)
    {
        await questions.DeleteAsync(id, cancellationToken);
        TempData.Toast("Đã xóa câu hỏi.");
        return RedirectToPage("/Questions/Index");
    }
}
