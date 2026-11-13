using AIpoweredVivaExamSystem.Business.Topics;
using AIpoweredVivaExamSystem.DataAccess.Models.Topics;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Presentation.Common;
using AIpoweredVivaExamSystem.Presentation.ViewModels;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;


namespace AIpoweredVivaExamSystem.Presentation.Pages.Questions;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,LECTURER")]
public sealed class TopicOptionsModel(TopicService topics) : PageModel
{
    public async Task<IActionResult> OnGetAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        if (subjectId == Guid.Empty)
            return new JsonResult(Array.Empty<object>());
        var items = await topics.ListAsync(new TopicListQuery { SubjectId = subjectId, PageSize = 100 }, cancellationToken);
        return new JsonResult(items.Items.Select(x => new { id = x.Id, name = x.Name }));
    }

    public IActionResult OnPost() => NotFound();
}
