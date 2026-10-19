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

namespace AIpoweredVivaExamSystem.Web.Controllers;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class DocumentsController(DocumentService documents, IQuestionBankStore store,
    ISubjectService subjects, ITopicService topics) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await LoadAsync(new(), ct));

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(21 * 1024 * 1024)]
    public async Task<IActionResult> Upload(DocumentsViewModel model, CancellationToken ct)
    {
        if (model.File is null || model.File.Length is 0 or > 20 * 1024 * 1024)
            ModelState.AddModelError(nameof(model.File), "Chọn PDF/PPTX tối đa 20 MB.");
        if (ModelState.IsValid)
        {
            try
            {
                using var stream = new MemoryStream(); await model.File!.CopyToAsync(stream, ct);
                var document = await documents.UploadAsync(model.SubjectId!.Value, stream.ToArray(), model.File.FileName, ct);
                return RedirectToAction(nameof(Generate), new { id = document.Id });
            }
            catch (Exception ex) when (ex is ResourceConflictException or DomainValidationException)
            { ModelState.AddModelError("", ex.Message); }
        }
        return View("Index", await LoadAsync(model, ct));
    }

    [HttpGet]
    public async Task<IActionResult> Generate(Guid id, CancellationToken ct)
    {
        var document = await store.FindDocumentAsync(id, ct) ?? throw new ResourceNotFoundException("Document not found.");
        return View(await LoadGenerateAsync(new() { DocumentId = document.Id, FileName = document.FileName }, document.SubjectId, ct));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate(GenerateQuestionsViewModel model, CancellationToken ct)
    {
        var document = await store.FindDocumentAsync(model.DocumentId, ct) ?? throw new ResourceNotFoundException("Document not found.");
        if (ModelState.IsValid)
        {
            try
            {
                var ids = await documents.GenerateAsync(model.DocumentId, model.TopicId, model.Instruction,
                    model.BloomLevel, model.Difficulty, model.Count, ct);
                TempData["Toast"] = $"Đã sinh {ids.Count} câu hỏi Nháp có nguồn trích dẫn. Thêm rubric trước khi duyệt.";
                return RedirectToAction("Index", "Questions", new { subjectId = document.SubjectId });
            }
            catch (Exception ex) when (ex is ResourceConflictException or DomainValidationException)
            { ModelState.AddModelError("", ex.Message); }
        }
        model.FileName = document.FileName;
        return View(await LoadGenerateAsync(model, document.SubjectId, ct));
    }
    private async Task<DocumentsViewModel> LoadAsync(DocumentsViewModel model, CancellationToken ct)
    {
        model.Documents = await documents.ListAsync(ct);
        model.Subjects = (await subjects.ListAsync(new SubjectListQuery { PageSize = 100 }, ct)).Items
            .Select(s => new SelectListItem($"{s.Code} — {s.Name}", s.Id.ToString()));
        return model;
    }
    private async Task<GenerateQuestionsViewModel> LoadGenerateAsync(GenerateQuestionsViewModel model, Guid subjectId, CancellationToken ct)
    {
        model.Topics = (await topics.ListAsync(new TopicListQuery { SubjectId = subjectId, PageSize = 100 }, ct)).Items
            .Select(t => new SelectListItem(t.Name, t.Id.ToString()));
        return model;
    }
}
