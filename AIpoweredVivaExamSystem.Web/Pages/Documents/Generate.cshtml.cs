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
public sealed class GenerateModel(DocumentService documents, IQuestionBankStore store, ITopicService topics) : ContentPageModel
{
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var document = await store.FindDocumentAsync(id, ct) ?? throw new ResourceNotFoundException("Document not found.");
        return RenderContent("/Pages/Documents/_GenerateContent.cshtml", await LoadGenerateAsync(new() { DocumentId = document.Id, FileName = document.FileName }, document.SubjectId, ct));
    }

    public async Task<IActionResult> OnPostAsync(GenerateQuestionsViewModel model, CancellationToken ct)
    {
        var document = await store.FindDocumentAsync(model.DocumentId, ct) ?? throw new ResourceNotFoundException("Document not found.");
        if (ModelState.IsValid)
        {
            try
            {
                var ids = await documents.GenerateAsync(model.DocumentId, model.TopicId, model.Instruction,
                    model.BloomLevel, model.Difficulty, model.Count, ct);
                TempData["Toast"] = $"Đã sinh {ids.Count} câu hỏi Nháp có nguồn trích dẫn. Thêm rubric trước khi duyệt.";
                return RedirectToPage("/Questions/Index", new { subjectId = document.SubjectId });
            }
            catch (Exception ex) when (ex is ResourceConflictException or DomainValidationException)
            { ModelState.AddModelError("", ex.Message); }
        }
        model.FileName = document.FileName;
        return RenderContent("/Pages/Documents/_GenerateContent.cshtml", await LoadGenerateAsync(model, document.SubjectId, ct));
    }

    private async Task<GenerateQuestionsViewModel> LoadGenerateAsync(GenerateQuestionsViewModel model, Guid subjectId, CancellationToken ct)
    {
        model.Topics = (await topics.ListAsync(new TopicListQuery { SubjectId = subjectId, PageSize = 100 }, ct)).Items
            .Select(t => new SelectListItem(t.Name, t.Id.ToString()));
        return model;
    }
}
