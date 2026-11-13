using AIpoweredVivaExamSystem.Business.VivaSessions;
using AIpoweredVivaExamSystem.DataAccess.Common;
using AIpoweredVivaExamSystem.DataAccess.Models.Common;
using AIpoweredVivaExamSystem.DataAccess.Models.VivaSessions;
using AIpoweredVivaExamSystem.Presentation.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AIpoweredVivaExamSystem.Presentation.Pages.VivaSessions;

[Authorize(Roles = "STUDENT")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AnswerModel(VivaInterviewService interview) : PageModel
{
    public async Task<IActionResult> OnPostAsync(
        Guid sessionId,
        Guid turnId,
        string? transcript,
        IFormFile? audioFile,
        int? audioDurationSeconds,
        string language = "vi",
        bool timedOut = false,
        CancellationToken ct = default)
    {
        byte[]? audioBytes = null;
        string? audioMime = null;

        if (audioFile is { Length: > 0 })
        {
            if (audioFile.Length > 20 * 1024 * 1024)
            {
                TempData["InterviewError"] = "File ghi âm vượt quá giới hạn 20 MB.";
                return RedirectToPage("/VivaSessions/Open", new { id = sessionId });
            }

            using var stream = new MemoryStream();
            await audioFile.CopyToAsync(stream, ct);
            audioBytes = stream.ToArray();
            audioMime = audioFile.ContentType;
        }

        try
        {
            var state = await interview.SubmitAnswerAsync(
                new SubmitAnswerRequest(sessionId, turnId, transcript, audioBytes, audioMime, audioDurationSeconds, language, timedOut),
                ct);

            if (WantsJson())
                return new JsonResult(state);

            if (state.CompletedAt.HasValue)
                TempData["SessionMessage"] = "Bạn đã hoàn thành toàn bộ câu hỏi của phiên thi vấn đáp.";

            return RedirectToPage("/VivaSessions/Open", new { id = sessionId });
        }
        catch (Exception ex) when (ex is DomainValidationException or ResourceConflictException)
        {
            if (WantsJson())
                return new JsonResult(new { error = ex.Message }) { StatusCode = StatusCodes.Status409Conflict };

            TempData["InterviewError"] = ex.Message;
            return RedirectToPage("/VivaSessions/Open", new { id = sessionId });
        }
    }

    public IActionResult OnGet() => NotFound();

    private bool WantsJson() =>
        Request.Headers.Accept.Any(h => h is not null && h.Contains("application/json", StringComparison.OrdinalIgnoreCase));
}

