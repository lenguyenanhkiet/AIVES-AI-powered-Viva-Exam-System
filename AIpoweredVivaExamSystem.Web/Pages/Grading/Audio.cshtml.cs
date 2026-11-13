using AIpoweredVivaExamSystem.Application.Grading;
using AIpoweredVivaExamSystem.Application.VivaSessions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace AIpoweredVivaExamSystem.Web.Pages.Grading;
[Authorize(Roles="ADMIN,LECTURER")]
[SkipStatusCodePages]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class AudioModel(GradingService grading,IAnswerAudioStorage storage) : PageModel
{
    public async Task<IActionResult> OnGetAsync(Guid id,CancellationToken ct)
    {
        try {
            var audio=await grading.AudioAsync(id,ct);
            var stream=await storage.OpenReadAsync(audio.FilePath,ct);
            return stream is null?NotFound():new FileStreamResult(stream,audio.MimeType) {EnableRangeProcessing=true};
        } catch(UnauthorizedAccessException) { return StatusCode(403); }
    }
}
