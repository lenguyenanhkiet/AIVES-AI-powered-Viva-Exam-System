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
[RequestSizeLimit(6 * 1024 * 1024)]
public sealed class PreviewModel(QuestionImportService imports, IMemoryCache cache, ICurrentUser actor) : ContentPageModel
{
    public async Task<IActionResult> OnPostAsync(ImportQuestionsViewModel model, CancellationToken ct)
    {
        if (model.File is null || model.File.Length is 0 or > 5 * 1024 * 1024)
            ModelState.AddModelError(nameof(model.File), "Chọn CSV/XLSX tối đa 5 MB.");
        if (!ModelState.IsValid) return RenderContent("/Pages/QuestionImports/_IndexContent.cshtml", model);
        try
        {
            using var stream = new MemoryStream(); await model.File!.CopyToAsync(stream, ct);
            var bytes = stream.ToArray();
            model.Preview = await imports.PreviewAsync(bytes, model.File.FileName, ct);
            if (model.Preview.Errors.Count == 0)
            {
                model.Token = Guid.NewGuid().ToString("N");
                // Keep only one preview per user so repeated uploads cannot retain unlimited files in memory.
                cache.Set("import:" + actor.Id, new PendingImport(actor.Id, model.Token, bytes, model.File.FileName), TimeSpan.FromMinutes(15));
            }
        }
        catch (ResourceConflictException ex) { ModelState.AddModelError("", ex.Message); }
        return RenderContent("/Pages/QuestionImports/_IndexContent.cshtml", model);
    }

    public IActionResult OnGet() => NotFound();
}
