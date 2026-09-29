using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Questions;
using AIpoweredVivaExamSystem.Application.Rubrics;
using AIpoweredVivaExamSystem.Application.Rubrics.DTOs;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Web.Common;
using AIpoweredVivaExamSystem.Web.Models;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace AIpoweredVivaExamSystem.Web.Controllers;

// A rubric belongs to exactly one question, so every page here starts from and returns to that question.
public sealed class RubricsController(IRubricService rubrics, IQuestionService questions) : Controller
{
    // GET: Rubrics/Create?questionId=
    public async Task<IActionResult> Create(Guid questionId, CancellationToken cancellationToken)
    {
        var question = await questions.GetAsync(questionId, cancellationToken);
        if (question.HasRubric)
            return RedirectToAction("Details", "Questions", new { id = questionId });
        ViewBag.Question = question;
        return View(new RubricFormViewModel
        {
            QuestionId = questionId,
            Criteria = [new CriterionFormViewModel()]
        });
    }

    // POST: Rubrics/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RubricFormViewModel model, CancellationToken cancellationToken)
    {
        model.Id = null;
        foreach (var criterion in model.Criteria)
            criterion.Id = null;
        if (Validate(model))
        {
            try
            {
                await rubrics.CreateAsync(new CreateRubricRequest(model.QuestionId, model.Name, model.Description,
                    Criteria(model)), cancellationToken);
                TempData.Toast("Đã tạo rubric. Câu hỏi đã có thể gửi duyệt.");
                return RedirectToAction("Details", "Questions", new { id = model.QuestionId });
            }
            catch (ResourceConflictException)
            {
                TempData.Toast("Câu hỏi này đã có rubric.", success: false);
                return RedirectToAction("Details", "Questions", new { id = model.QuestionId });
            }
            catch (Exception exception) when (TryAddError(exception)) { }
        }
        ViewBag.Question = await questions.GetAsync(model.QuestionId, cancellationToken);
        return View(model);
    }

    // GET: Rubrics/Edit/{id}
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var rubric = await rubrics.GetAsync(id, cancellationToken);
        ViewBag.Question = await questions.GetAsync(rubric.QuestionId, cancellationToken);
        return View(RubricFormViewModel.From(rubric));
    }

    // POST: Rubrics/Edit/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, RubricFormViewModel model, CancellationToken cancellationToken)
    {
        var current = await rubrics.GetAsync(id, cancellationToken);
        model.Id = id;
        model.QuestionId = current.QuestionId;
        if (Validate(model))
        {
            try
            {
                await rubrics.UpdateAsync(id, new UpdateRubricRequest(model.Name, model.Description, Criteria(model)),
                    cancellationToken);
                TempData.Toast("Đã lưu rubric.");
                return RedirectToAction("Details", "Questions", new { id = model.QuestionId });
            }
            catch (ResourceConflictException)
            {
                ModelState.AddModelError(string.Empty, "Rubric vừa được người khác sửa. Tải lại trang rồi thử lại.");
            }
            catch (Exception exception) when (TryAddError(exception)) { }
        }
        ViewBag.Question = await questions.GetAsync(model.QuestionId, cancellationToken);
        return View(model);
    }

    // POST: Rubrics/Delete/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var rubric = await rubrics.GetAsync(id, cancellationToken);
        // An approved question must keep its rubric; it has to go back to Draft (by editing) first.
        var question = await questions.GetAsync(rubric.QuestionId, cancellationToken);
        if (question.Status == QuestionStatus.Approved)
        {
            TempData.Toast("Không thể xóa rubric của câu hỏi đã duyệt. Sửa câu hỏi để đưa về Nháp trước.", success: false);
            return RedirectToAction("Details", "Questions", new { id = rubric.QuestionId });
        }
        await rubrics.DeleteAsync(id, cancellationToken);
        TempData.Toast("Đã xóa rubric.");
        return RedirectToAction("Details", "Questions", new { id = rubric.QuestionId });
    }

    private bool Validate(RubricFormViewModel model)
    {
        if (model.Criteria.Count == 0)
            ModelState.AddModelError(string.Empty, "Rubric cần ít nhất một tiêu chí.");
        else if (model.Criteria.Sum(x => x.MaxScore ?? 0) > 999.99m)
            ModelState.AddModelError(string.Empty, "Tổng điểm không được vượt quá 999.99.");
        return ModelState.IsValid;
    }

    // Criteria are ordered as they appear on the form.
    private static List<CriterionRequest> Criteria(RubricFormViewModel model) =>
        model.Criteria.Select((x, i) => new CriterionRequest(x.Name, x.Description, x.ExpectedConcepts,
            x.MaxScore!.Value, i + 1, x.Id)).ToList();

    private bool TryAddError(Exception exception)
    {
        switch (exception)
        {
            case ValidationException validation:
                ModelState.AddErrors(validation);
                return true;
            case DomainValidationException domain:
                ModelState.AddModelError(string.Empty, domain.Message);
                return true;
            default:
                return false;
        }
    }
}
