using System.Text;
using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.QuestionBank;
using AIpoweredVivaExamSystem.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

using AIpoweredVivaExamSystem.Web.Common;

namespace AIpoweredVivaExamSystem.Web.Pages.QuestionImports;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class ConfirmModel(QuestionImportService imports, IMemoryCache cache, ICurrentUser actor) : ContentPageModel
{
    public async Task<IActionResult> OnPostAsync(string token, CancellationToken ct)
    {
        if (!cache.TryGetValue<PendingImport>("import:" + actor.Id, out var pending) || pending!.UserId != actor.Id || pending.Token != token)
        { ModelState.AddModelError("", "Bản xem trước đã hết hạn. Hãy tải file lại."); return RenderContent("/Pages/QuestionImports/_IndexContent.cshtml", new ImportQuestionsViewModel()); }
        try
        {
            var count = await imports.ImportAsync(pending.Content, pending.FileName, ct);
            cache.Remove("import:" + actor.Id);
            TempData["Toast"] = $"Đã nhập {count} câu hỏi Nháp.";
            return RedirectToPage("/Questions/Index");
        }
        catch (ResourceConflictException ex)
        { ModelState.AddModelError("", ex.Message); return RenderContent("/Pages/QuestionImports/_IndexContent.cshtml", new ImportQuestionsViewModel()); }
    }

    public IActionResult OnGet() => NotFound();
}
