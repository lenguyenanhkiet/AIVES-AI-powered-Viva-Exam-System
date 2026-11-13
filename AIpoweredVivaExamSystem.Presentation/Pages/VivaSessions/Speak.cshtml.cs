using AIpoweredVivaExamSystem.Business.VivaSessions;
using AIpoweredVivaExamSystem.DataAccess.Models.Common;
using AIpoweredVivaExamSystem.Presentation.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AIpoweredVivaExamSystem.Presentation.Pages.VivaSessions;

[Authorize(Roles = "STUDENT")]
[SkipStatusCodePages]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SpeakModel(VivaQuestionSpeechService questionSpeech) : PageModel
{
    public async Task<IActionResult> OnGetAsync(Guid sessionId, Guid turnId, CancellationToken ct)
    {
        try
        {
            var audio = await questionSpeech.SpeakAsync(sessionId, turnId, ct);
            return audio is { Length: > 0 } ? File(audio, "audio/mpeg") : StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ResourceNotFoundException) { return NotFound(); }
    }
}
