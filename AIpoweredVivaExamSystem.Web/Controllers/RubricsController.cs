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
    // GET: /Rubrics/Details/{questionId} or /Rubrics/Details?questionId=
    [HttpGet]
    [Route("Rubrics/Details/{questionId:guid}")]
    [Route("Rubrics/Details")]
    public async Task<IActionResult> Details(Guid? questionId, [FromRoute] Guid? id, CancellationToken cancellationToken)
    {
        var targetQuestionId = questionId ?? id;
        if (!targetQuestionId.HasValue || targetQuestionId.Value == Guid.Empty)
            return NotFound();

        var question = await questions.GetAsync(targetQuestionId.Value, cancellationToken);
        var rubric = await rubrics.GetByQuestionIdAsync(targetQuestionId.Value, cancellationToken);

        return View(new RubricDetailsViewModel
        {
            Question = question,
            Rubric = rubric
        });
    }

    // GET: /Rubrics/Create/{questionId} or /Rubrics/Create?questionId=
    [HttpGet]
    [Route("Rubrics/Create/{questionId:guid}")]
    [Route("Rubrics/Create")]
    public async Task<IActionResult> Create(Guid? questionId, [FromRoute] Guid? id, CancellationToken cancellationToken)
    {
        var targetQuestionId = questionId ?? id;
        if (!targetQuestionId.HasValue || targetQuestionId.Value == Guid.Empty)
            return NotFound();

        var question = await questions.GetAsync(targetQuestionId.Value, cancellationToken);
        if (question.HasRubric)
            return RedirectToAction(nameof(Details), new { questionId = targetQuestionId.Value });

        ViewBag.Question = question;
        return View(new RubricFormViewModel
        {
            QuestionId = targetQuestionId.Value,
            Criteria = [new CriterionFormViewModel()]
        });
    }

    // POST: /Rubrics/Create
    [HttpPost]
    [Route("Rubrics/Create")]
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
                return RedirectToAction(nameof(Details), new { questionId = model.QuestionId });
            }
            catch (ResourceConflictException)
            {
                TempData.Toast("Câu hỏi này đã có rubric.", success: false);
                return RedirectToAction(nameof(Details), new { questionId = model.QuestionId });
            }
            catch (Exception exception) when (TryAddError(exception)) { }
        }

        ViewBag.Question = await questions.GetAsync(model.QuestionId, cancellationToken);
        return View(model);
    }

    // GET: /Rubrics/Edit/{questionId} or /Rubrics/Edit/{rubricId}
    [HttpGet]
    [Route("Rubrics/Edit/{id:guid}")]
    [Route("Rubrics/Edit")]
    public async Task<IActionResult> Edit([FromRoute] Guid? id, [FromQuery] Guid? questionId, CancellationToken cancellationToken)
    {
        var targetId = id ?? questionId;
        if (!targetId.HasValue || targetId.Value == Guid.Empty)
            return NotFound();

        RubricResponse? rubric = null;
        try
        {
            rubric = await rubrics.GetAsync(targetId.Value, cancellationToken);
        }
        catch (ResourceNotFoundException)
        {
            rubric = await rubrics.GetByQuestionIdAsync(targetId.Value, cancellationToken);
        }

        if (rubric is null)
            throw new ResourceNotFoundException("Rubric was not found.");

        ViewBag.Question = await questions.GetAsync(rubric.QuestionId, cancellationToken);
        return View(RubricFormViewModel.From(rubric));
    }

    // POST: /Rubrics/Edit or /Rubrics/Edit/{id}
    [HttpPost]
    [Route("Rubrics/Edit/{id:guid?}")]
    [Route("Rubrics/Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid? id, RubricFormViewModel model, CancellationToken cancellationToken)
    {
        var targetId = model.Id ?? id;
        if (!targetId.HasValue || targetId.Value == Guid.Empty)
        {
            var byQuestion = await rubrics.GetByQuestionIdAsync(model.QuestionId, cancellationToken);
            if (byQuestion is not null)
                targetId = byQuestion.Id;
        }

        if (!targetId.HasValue || targetId.Value == Guid.Empty)
            throw new ResourceNotFoundException("Rubric was not found.");

        RubricResponse current;
        try
        {
            current = await rubrics.GetAsync(targetId.Value, cancellationToken);
        }
        catch (ResourceNotFoundException)
        {
            current = await rubrics.GetByQuestionIdAsync(targetId.Value, cancellationToken)
                ?? throw new ResourceNotFoundException("Rubric was not found.");
        }

        model.Id = current.Id;
        model.QuestionId = current.QuestionId;

        if (Validate(model))
        {
            try
            {
                await rubrics.UpdateAsync(current.Id, new UpdateRubricRequest(model.Name, model.Description, Criteria(model)),
                    cancellationToken);
                TempData.Toast("Đã lưu rubric.");
                return RedirectToAction(nameof(Details), new { questionId = model.QuestionId });
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

    // POST: /Rubrics/Delete/{id}
    [HttpPost]
    [Route("Rubrics/Delete/{id:guid}")]
    [Route("Rubrics/Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        RubricResponse rubric;
        try
        {
            rubric = await rubrics.GetAsync(id, cancellationToken);
        }
        catch (ResourceNotFoundException)
        {
            rubric = await rubrics.GetByQuestionIdAsync(id, cancellationToken)
                ?? throw new ResourceNotFoundException("Rubric was not found.");
        }

        // An approved question must keep its rubric; it has to go back to Draft (by editing) first.
        var question = await questions.GetAsync(rubric.QuestionId, cancellationToken);
        if (question.Status == QuestionStatus.Approved)
        {
            TempData.Toast("Không thể xóa rubric của câu hỏi đã duyệt. Sửa câu hỏi để đưa về Nháp trước.", success: false);
            return RedirectToAction(nameof(Details), new { questionId = rubric.QuestionId });
        }

        await rubrics.DeleteAsync(rubric.Id, cancellationToken);
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
