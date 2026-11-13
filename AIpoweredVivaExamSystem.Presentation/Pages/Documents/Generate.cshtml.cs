using AIpoweredVivaExamSystem.Business.Topics;
using AIpoweredVivaExamSystem.Business.QuestionBank;
using AIpoweredVivaExamSystem.DataAccess.Common;
using AIpoweredVivaExamSystem.DataAccess.Models.Common;
using AIpoweredVivaExamSystem.DataAccess.Models.Topics;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Presentation.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

using AIpoweredVivaExamSystem.Presentation.Common;

namespace AIpoweredVivaExamSystem.Presentation.Pages.Documents;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class GenerateModel(QuestionBankReadService questionBank, DocumentService documents, TopicService topics) : PageModel
{
    [BindProperty]
    public GenerateQuestionsViewModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var document = await questionBank.FindDocumentAsync(id, ct) ?? throw new ResourceNotFoundException("Document not found.");
        Input = await LoadGenerateAsync(new() { DocumentId = document.Id, FileName = document.FileName }, document.SubjectId, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var document = await questionBank.FindDocumentAsync(Input.DocumentId, ct) ?? throw new ResourceNotFoundException("Document not found.");
        if (ModelState.IsValid)
        {
            try
            {
                var ids = await documents.GenerateAsync(Input.DocumentId, Input.TopicId, Input.Instruction,
                    Input.BloomLevel, Input.Difficulty, Input.Count, ct);
                TempData["Toast"] = $"Đã sinh {ids.Count} câu hỏi Nháp có nguồn trích dẫn. Thêm rubric trước khi duyệt.";
                return RedirectToPage("/Questions/Index", new { subjectId = document.SubjectId });
            }
            catch (Exception ex) when (ex is ResourceConflictException or DomainValidationException)
            { ModelState.AddModelError("", ex.Message); }
        }
        Input.FileName = document.FileName;
        Input = await LoadGenerateAsync(Input, document.SubjectId, ct);
        return Page();
    }

    private async Task<GenerateQuestionsViewModel> LoadGenerateAsync(GenerateQuestionsViewModel model, Guid subjectId, CancellationToken ct)
    {
        model.Topics = (await topics.ListAsync(new TopicListQuery { SubjectId = subjectId, PageSize = 100 }, ct)).Items
            .Select(t => new SelectListItem(t.Name, t.Id.ToString()));
        return model;
    }
}
