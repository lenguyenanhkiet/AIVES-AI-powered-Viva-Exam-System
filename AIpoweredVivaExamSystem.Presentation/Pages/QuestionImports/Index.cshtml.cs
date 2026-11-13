using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;
using AIpoweredVivaExamSystem.Presentation.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

using AIpoweredVivaExamSystem.Presentation.Common;

namespace AIpoweredVivaExamSystem.Presentation.Pages.QuestionImports;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class IndexModel : PageModel
{
    [BindProperty]
    public ImportQuestionsViewModel Input { get; set; } = new();

    public IActionResult OnGet()
    {
        Input = new ImportQuestionsViewModel();
        return Page();
    }

    public IActionResult OnPost() => NotFound();
}
