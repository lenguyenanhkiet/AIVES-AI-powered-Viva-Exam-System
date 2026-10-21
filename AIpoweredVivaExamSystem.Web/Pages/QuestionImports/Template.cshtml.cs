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
public sealed class TemplateModel : ContentPageModel
{
    public IActionResult OnGet() => File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(
        "SubjectCode,Topic,Content,ExpectedAnswer,BloomLevel,Difficulty\r\nPRN222,,Explain MVC.,Model View Controller,Understand,Medium\r\n")).ToArray(), "text/csv", "question-import-template.csv");

    public IActionResult OnPost() => NotFound();
}
