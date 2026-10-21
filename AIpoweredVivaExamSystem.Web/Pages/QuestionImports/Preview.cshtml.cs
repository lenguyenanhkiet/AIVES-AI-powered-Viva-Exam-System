using Microsoft.AspNetCore.Mvc.RazorPages;
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
public sealed class PreviewModel(QuestionImportService imports, IMemoryCache cache, ICurrentUser actor) : PageModel
{
    [BindProperty]
    public ImportQuestionsViewModel Input { get; set; } = new();

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (Input.File is null || Input.File.Length is 0 or > 5 * 1024 * 1024)
            ModelState.AddModelError($"Input.{nameof(Input.File)}", "Chọn CSV/XLSX tối đa 5 MB.");
        if (!ModelState.IsValid) return Page();
        try
        {
            using var stream = new MemoryStream(); await Input.File!.CopyToAsync(stream, ct);
            var bytes = stream.ToArray();
            Input.Preview = await imports.PreviewAsync(bytes, Input.File.FileName, ct);
            if (Input.Preview.Errors.Count == 0)
            {
                Input.Token = Guid.NewGuid().ToString("N");
                // Keep only one preview per user so repeated uploads cannot retain unlimited files in memory.
                cache.Set("import:" + actor.Id, new PendingImport(actor.Id, Input.Token, bytes, Input.File.FileName), TimeSpan.FromMinutes(15));
            }
        }
        catch (ResourceConflictException ex) { ModelState.AddModelError("", ex.Message); }
        return Page();
    }

    public IActionResult OnGet() => NotFound();
}
