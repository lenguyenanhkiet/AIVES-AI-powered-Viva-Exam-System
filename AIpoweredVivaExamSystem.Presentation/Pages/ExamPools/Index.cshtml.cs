using AIpoweredVivaExamSystem.Business.Exams;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Presentation.Common;
using AIpoweredVivaExamSystem.Presentation.ViewModels.ExamPools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace AIpoweredVivaExamSystem.Presentation.Pages.ExamPools;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class IndexModel(ExamPoolService pools) : PageModel
{
    public ExamPoolViewModel Vm { get; private set; } = default!;

    [BindProperty]
    public ExamPoolSelectionViewModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        Vm = await PageAsync(id, new(), ct);
        return Page();
    }

    private async Task<ExamPoolViewModel> PageAsync(Guid examId, ExamPoolSelectionViewModel form, CancellationToken ct) =>
        new() { Page = await pools.GetPageAsync(examId, ct), Form = form };

    public IActionResult OnPost() => NotFound();
}
