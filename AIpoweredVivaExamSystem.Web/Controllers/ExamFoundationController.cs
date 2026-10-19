using AIpoweredVivaExamSystem.Application.Exams;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIpoweredVivaExamSystem.Web.Controllers;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class ExamFoundationController(ExamFoundationService foundation) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await foundation.ListAsync(ct));
    [HttpGet]
    public async Task<IActionResult> Counts(Guid id, CancellationToken ct) => Json(await foundation.CountsAsync(id, ct));
}
