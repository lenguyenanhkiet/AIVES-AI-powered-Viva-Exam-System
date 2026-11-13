using AIpoweredVivaExamSystem.Business.VivaSessions;
using AIpoweredVivaExamSystem.DataAccess.Common;
using AIpoweredVivaExamSystem.DataAccess.Models.Common;
using AIpoweredVivaExamSystem.Presentation.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AIpoweredVivaExamSystem.Presentation.Pages.VivaSessions;

[Authorize(Roles = "STUDENT")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TranscribeModel(VivaInterviewService interview) : PageModel
{
    public async Task<IActionResult> OnPostAsync(
        Guid sessionId,
        IFormFile? audioFile,
        string language = "vi",
        CancellationToken ct = default)
    {
        if (audioFile is null || audioFile.Length == 0)
            return new JsonResult(new { error = "Chưa có dữ liệu âm thanh để nhận diện." }) { StatusCode = StatusCodes.Status400BadRequest };

        if (audioFile.Length > 20 * 1024 * 1024)
            return new JsonResult(new { error = "File ghi âm vượt quá giới hạn 20 MB." }) { StatusCode = StatusCodes.Status409Conflict };

        try
        {
            using var ms = new MemoryStream();
            await audioFile.CopyToAsync(ms, ct);
            var transcript = await interview.TranscribeAudioAsync(sessionId, ms.ToArray(), audioFile.FileName, language, ct);
            return new JsonResult(new { transcript });
        }
        catch (Exception ex) when (ex is DomainValidationException or ResourceConflictException)
        {
            return new JsonResult(new { error = ex.Message }) { StatusCode = StatusCodes.Status409Conflict };
        }
    }

    public IActionResult OnGet() => NotFound();
}

