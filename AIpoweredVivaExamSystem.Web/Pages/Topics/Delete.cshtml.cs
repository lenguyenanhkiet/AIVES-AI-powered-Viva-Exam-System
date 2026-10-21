using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Questions;
using AIpoweredVivaExamSystem.Application.Questions.DTOs;
using AIpoweredVivaExamSystem.Application.Subjects;
using AIpoweredVivaExamSystem.Application.Subjects.DTOs;
using AIpoweredVivaExamSystem.Application.Topics;
using AIpoweredVivaExamSystem.Application.Topics.DTOs;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Web.Common;
using AIpoweredVivaExamSystem.Web.ViewModels;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;


namespace AIpoweredVivaExamSystem.Web.Pages.Topics;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,LECTURER")]
public sealed class DeleteModel(ITopicService topics, ISubjectService subjects, IQuestionService questions) : PageModel
{
    public TopicDetailsViewModel Vm { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var topic = await topics.GetAsync(id, cancellationToken);
        (await SubjectMapAsync(cancellationToken)).TryGetValue(topic.SubjectId, out var subject);
        var topicQuestions = await questions.ListAsync(new QuestionListQuery { TopicId = id, PageSize = 10 }, cancellationToken);
        Vm = new TopicDetailsViewModel { Topic = topic, Subject = subject, Questions = topicQuestions };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken cancellationToken)
    {
        var topic = await topics.GetAsync(id, cancellationToken);
        try
        {
            await topics.DeleteAsync(id, cancellationToken);
            TempData.Toast($"Đã xóa chủ đề \"{topic.Name}\".");
        }
        catch (ResourceConflictException)
        {
            TempData.Toast("Không thể xóa: chủ đề vẫn còn câu hỏi. Hãy xóa hoặc chuyển chúng trước.", success: false);
        }
        return RedirectToPage("/Subjects/Details", new { id = topic.SubjectId });
    }

    private async Task<IReadOnlyDictionary<Guid, SubjectResponse>> SubjectMapAsync(CancellationToken cancellationToken) =>
        (await subjects.ListAsync(new SubjectListQuery { PageSize = 100 }, cancellationToken)).Items.ToDictionary(x => x.Id);
}
