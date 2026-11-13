using AIpoweredVivaExamSystem.Business.QuestionBank;
using AIpoweredVivaExamSystem.Business.Authentication;
using AIpoweredVivaExamSystem.DataAccess.Models.Common;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;
using AIpoweredVivaExamSystem.Presentation.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

using AIpoweredVivaExamSystem.Presentation.Common;

namespace AIpoweredVivaExamSystem.Presentation.Pages.QuestionImports;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class ConfirmModel(QuestionImportService imports, IMemoryCache cache, ICurrentUser actor) : PageModel
{
    [BindProperty]
    public ImportQuestionsViewModel Input { get; set; } = new();

    public async Task<IActionResult> OnPostAsync(string token, CancellationToken ct)
    {
        if (!cache.TryGetValue<PendingImport>("import:" + actor.Id, out var pending) || pending!.UserId != actor.Id || pending.Token != token)
        {
            ModelState.AddModelError("", "Bản xem trước đã hết hạn. Hãy tải file lại.");
            Input = new ImportQuestionsViewModel();
            return Page();
        }
        try
        {
            var count = await imports.ImportAsync(pending.Content, pending.FileName, ct);
            cache.Remove("import:" + actor.Id);
            TempData["Toast"] = $"Đã nhập {count} câu hỏi Nháp.";
            return RedirectToPage("/Questions/Index");
        }
        catch (ResourceConflictException ex)
        {
            ModelState.AddModelError("", ex.Message);
            Input = new ImportQuestionsViewModel();
            return Page();
        }
    }

    public IActionResult OnGet() => NotFound();
}
