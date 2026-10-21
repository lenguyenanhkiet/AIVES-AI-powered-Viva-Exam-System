using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics;
using AIpoweredVivaExamSystem.Application.Questions;
using AIpoweredVivaExamSystem.Application.Questions.DTOs;
using AIpoweredVivaExamSystem.Application.Rubrics;
using AIpoweredVivaExamSystem.Application.Rubrics.DTOs;
using AIpoweredVivaExamSystem.Application.Subjects;
using AIpoweredVivaExamSystem.Application.Subjects.DTOs;
using AIpoweredVivaExamSystem.Application.Topics;
using AIpoweredVivaExamSystem.Application.Topics.DTOs;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

using AIpoweredVivaExamSystem.Web.Common;

namespace AIpoweredVivaExamSystem.Web.Pages.Home;

[AllowAnonymous]
public sealed class StatusCodeModel : PageModel
{
    public int Vm { get; private set; } = default!;

    public IActionResult OnGet(int code)
    {
        Response.StatusCode = code;
        Vm = code;
        return Page();
    }

    public IActionResult OnPost(int code) => OnGet(code);
}
