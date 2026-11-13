using AIpoweredVivaExamSystem.DataAccess.Entities;
using AIpoweredVivaExamSystem.DataAccess.Enums;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;
using AIpoweredVivaExamSystem.Presentation.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

using AIpoweredVivaExamSystem.Presentation.Common;

namespace AIpoweredVivaExamSystem.Presentation.Pages.QuestionImports;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class TemplateModel : PageModel
{
    public IActionResult OnGet() => File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(
        "SubjectCode,Topic,Content,ExpectedAnswer,BloomLevel,Difficulty\r\nPRN222,,Explain MVC.,Model View Controller,Understand,Medium\r\n")).ToArray(), "text/csv", "question-import-template.csv");

    public IActionResult OnPost() => NotFound();
}
