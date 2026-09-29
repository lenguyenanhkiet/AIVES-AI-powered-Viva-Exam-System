using System.ComponentModel.DataAnnotations;
using AIpoweredVivaExamSystem.Application.Questions.DTOs;
using AIpoweredVivaExamSystem.Application.Rubrics.DTOs;
using AIpoweredVivaExamSystem.Application.Subjects.DTOs;
using AIpoweredVivaExamSystem.Application.Topics.DTOs;
using AIpoweredVivaExamSystem.Domain.Enums;

namespace AIpoweredVivaExamSystem.Web.Models;

public sealed class TopicFormViewModel
{
    public Guid? Id { get; set; }

    [Display(Name = "Môn học")]
    [Required(ErrorMessage = "Vui lòng chọn môn học.")]
    public Guid? SubjectId { get; set; }

    [Display(Name = "Tên chủ đề")]
    [Required(ErrorMessage = "Vui lòng nhập tên chủ đề.")]
    [StringLength(255, ErrorMessage = "Tên chủ đề tối đa 255 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [Display(Name = "Trạng thái")]
    public AcademicStatus Status { get; set; } = AcademicStatus.Active;

    public static TopicFormViewModel From(TopicResponse topic) => new()
    {
        Id = topic.Id, SubjectId = topic.SubjectId, Name = topic.Name,
        Description = topic.Description, Status = topic.Status
    };
}

public sealed class TopicIndexViewModel
{
    public required PagedResponse<TopicResponse> Page { get; init; }
    public required TopicListQuery Query { get; init; }
    public required IReadOnlyDictionary<Guid, SubjectResponse> Subjects { get; init; }
}

public sealed class TopicDetailsViewModel
{
    public required TopicResponse Topic { get; init; }
    public SubjectResponse? Subject { get; init; }
    public required PagedResponse<QuestionResponse> Questions { get; init; }
}
