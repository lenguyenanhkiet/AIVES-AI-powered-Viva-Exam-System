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
public sealed class ScribeTokenModel(VivaScribeTokenService scribeTokens) : PageModel
{
    public async Task<IActionResult> OnPostAsync(Guid sessionId, string language = "vi", CancellationToken ct = default)
    {
        try
        {
            var token = await scribeTokens.CreateAsync(sessionId, language, ct);
            return token is null ? StatusCode(StatusCodes.Status503ServiceUnavailable) : new JsonResult(token);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ResourceNotFoundException) { return NotFound(); }
        catch (ResourceConflictException) { return StatusCode(StatusCodes.Status409Conflict); }
    }
}
