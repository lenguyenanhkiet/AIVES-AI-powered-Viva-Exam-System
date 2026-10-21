using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.QuestionBank;
using AIpoweredVivaExamSystem.Application.Subjects;
using AIpoweredVivaExamSystem.Application.Subjects.DTOs;
using AIpoweredVivaExamSystem.Application.Topics;
using AIpoweredVivaExamSystem.Application.Topics.DTOs;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

using AIpoweredVivaExamSystem.Web.Common;

namespace AIpoweredVivaExamSystem.Web.Pages.Documents;

[Authorize(Roles = "ADMIN,LECTURER")]
[RequestSizeLimit(21 * 1024 * 1024)]
public sealed class UploadModel(DocumentService documents, ISubjectService subjects) : PageModel
{
    [BindProperty]
    public DocumentsViewModel Input { get; set; } = new();

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (Input.File is null || Input.File.Length is 0 or > 20 * 1024 * 1024)
            ModelState.AddModelError($"Input.{nameof(Input.File)}", "Chọn PDF/PPTX tối đa 20 MB.");
        if (ModelState.IsValid)
        {
            try
            {
                using var stream = new MemoryStream(); await Input.File!.CopyToAsync(stream, ct);
                var document = await documents.UploadAsync(Input.SubjectId!.Value, stream.ToArray(), Input.File.FileName, ct);
                return RedirectToPage("/Documents/Generate", new { id = document.Id });
            }
            catch (Exception ex) when (ex is ResourceConflictException or DomainValidationException)
            { ModelState.AddModelError("", ex.Message); }
        }
        Input = await LoadAsync(Input, ct);
        return Page();
    }

    private async Task<DocumentsViewModel> LoadAsync(DocumentsViewModel model, CancellationToken ct)
    {
        model.Documents = await documents.ListAsync(ct);
        model.Subjects = (await subjects.ListAsync(new SubjectListQuery { PageSize = 100 }, ct)).Items
            .Select(s => new SelectListItem($"{s.Code} — {s.Name}", s.Id.ToString()));
        return model;
    }

    public IActionResult OnGet() => NotFound();
}
