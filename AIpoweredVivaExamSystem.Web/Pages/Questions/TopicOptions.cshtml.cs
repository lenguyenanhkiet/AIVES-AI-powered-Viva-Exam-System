using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Questions;
using AIpoweredVivaExamSystem.Application.Questions.DTOs;
using AIpoweredVivaExamSystem.Application.Rubrics;
using AIpoweredVivaExamSystem.Application.Subjects;
using AIpoweredVivaExamSystem.Application.Subjects.DTOs;
using AIpoweredVivaExamSystem.Application.Topics;
using AIpoweredVivaExamSystem.Application.Topics.DTOs;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Web.Common;
using AIpoweredVivaExamSystem.Web.ViewModels;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;


namespace AIpoweredVivaExamSystem.Web.Pages.Questions;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,LECTURER")]
public sealed class TopicOptionsModel(ITopicService topics) : ContentPageModel
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
