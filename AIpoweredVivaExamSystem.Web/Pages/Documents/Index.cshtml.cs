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
public sealed class IndexModel(DocumentService documents, ISubjectService subjects) : PageModel
{
    [BindProperty]
    public DocumentsViewModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Input = await LoadAsync(new(), ct);
        return Page();
    }

    private async Task<DocumentsViewModel> LoadAsync(DocumentsViewModel model, CancellationToken ct)
    {
        model.Documents = await documents.ListAsync(ct);
        model.Subjects = (await subjects.ListAsync(new SubjectListQuery { PageSize = 100 }, ct)).Items
            .Select(s => new SelectListItem($"{s.Code} — {s.Name}", s.Id.ToString()));
        return model;
    }

    public IActionResult OnPost() => NotFound();
}
