using System.Text;
using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.QuestionBank;
using AIpoweredVivaExamSystem.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace AIpoweredVivaExamSystem.Web.Controllers;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class QuestionImportsController(QuestionImportService imports, IMemoryCache cache, ICurrentUser actor) : Controller
{
    private sealed record PendingImport(Guid UserId, string Token, byte[] Content, string FileName);
    [HttpGet] public IActionResult Index() => View(new ImportQuestionsViewModel());
    [HttpGet] public IActionResult Template() => File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(
        "SubjectCode,Topic,Content,ExpectedAnswer,BloomLevel,Difficulty\r\nPRN222,,Explain MVC.,Model View Controller,Understand,Medium\r\n")).ToArray(), "text/csv", "question-import-template.csv");

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Preview(ImportQuestionsViewModel model, CancellationToken ct)
    {
        if (model.File is null || model.File.Length is 0 or > 5 * 1024 * 1024)
            ModelState.AddModelError(nameof(model.File), "Chọn CSV/XLSX tối đa 5 MB.");
        if (!ModelState.IsValid) return View("Index", model);
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
        return View("Index", model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(string token, CancellationToken ct)
    {
        if (!cache.TryGetValue<PendingImport>("import:" + actor.Id, out var pending) || pending!.UserId != actor.Id || pending.Token != token)
        { ModelState.AddModelError("", "Bản xem trước đã hết hạn. Hãy tải file lại."); return View("Index", new ImportQuestionsViewModel()); }
        try
        {
            var count = await imports.ImportAsync(pending.Content, pending.FileName, ct);
            cache.Remove("import:" + actor.Id);
            TempData["Toast"] = $"Đã nhập {count} câu hỏi Nháp.";
            return RedirectToAction("Index", "Questions");
        }
        catch (ResourceConflictException ex)
        { ModelState.AddModelError("", ex.Message); return View("Index", new ImportQuestionsViewModel()); }
    }
}
