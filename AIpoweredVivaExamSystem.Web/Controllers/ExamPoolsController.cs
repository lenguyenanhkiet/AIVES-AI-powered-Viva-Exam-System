using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Web.Common;
using AIpoweredVivaExamSystem.Web.ViewModels.ExamPools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIpoweredVivaExamSystem.Web.Controllers;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class ExamPoolsController(ExamPoolService pools) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(Guid id, CancellationToken ct) => View(await PageAsync(id, new(), ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(Guid id, ExamPoolSelectionViewModel form, CancellationToken ct)
    {
        var page = await PageAsync(id, form, ct);
        if (!ModelState.IsValid) return View(nameof(Index), page);
        try
        {
            await pools.AddAsync(id, form.QuestionIds, ct);
            TempData.Toast("Đã thêm câu hỏi vào pool.");
            return RedirectToAction(nameof(Index), new { id });
        }
        catch (Exception ex) when (ex is DomainValidationException or ResourceConflictException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(nameof(Index), await PageAsync(id, form, ct));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(Guid id, ExamPoolSelectionViewModel form, CancellationToken ct)
    {
        var page = await PageAsync(id, form, ct);
        if (!ModelState.IsValid) return View(nameof(Index), page);
        try
        {
            await pools.RemoveAsync(id, form.QuestionIds, ct);
            TempData.Toast("Đã bỏ câu hỏi khỏi pool.");
            return RedirectToAction(nameof(Index), new { id });
        }
        catch (ResourceConflictException ex)
        {
            TempData.Toast(ex.Message, success: false);
            return RedirectToAction(nameof(Index), new { id });
        }
        catch (DomainValidationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(nameof(Index), await PageAsync(id, form, ct));
        }
    }

    private async Task<ExamPoolViewModel> PageAsync(Guid examId, ExamPoolSelectionViewModel form, CancellationToken ct) =>
        new() { Page = await pools.GetPageAsync(examId, ct), Form = form };
}
