using System.Text;
using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.QuestionBank;
using AIpoweredVivaExamSystem.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

using AIpoweredVivaExamSystem.Web.Common;

namespace AIpoweredVivaExamSystem.Web.Pages.QuestionImports;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class IndexModel : ContentPageModel
{
    public IActionResult OnGet() => RenderContent("/Pages/QuestionImports/_IndexContent.cshtml", new ImportQuestionsViewModel());

    public IActionResult OnPost() => NotFound();
}
