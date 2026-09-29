using System.ComponentModel.DataAnnotations;
using AIpoweredVivaExamSystem.Application.Rubrics.DTOs;
using AIpoweredVivaExamSystem.Application.Subjects.DTOs;
using AIpoweredVivaExamSystem.Application.Topics.DTOs;
using AIpoweredVivaExamSystem.Domain.Enums;

namespace AIpoweredVivaExamSystem.Web.Models;

public sealed class SubjectFormViewModel
{
    public Guid? Id { get; set; }

    [Display(Name = "Mã môn")]
    [Required(ErrorMessage = "Vui lòng nhập mã môn.")]
    [StringLength(50, ErrorMessage = "Mã môn tối đa 50 ký tự.")]
    public string Code { get; set; } = string.Empty;

    [Display(Name = "Tên môn học")]
    [Required(ErrorMessage = "Vui lòng nhập tên môn học.")]
    [StringLength(255, ErrorMessage = "Tên môn học tối đa 255 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [Display(Name = "Trạng thái")]
    public AcademicStatus Status { get; set; } = AcademicStatus.Active;

    public static SubjectFormViewModel From(SubjectResponse subject) => new()
    {
        Id = subject.Id, Code = subject.Code, Name = subject.Name,
        Description = subject.Description, Status = subject.Status
    };
}

public sealed class SubjectIndexViewModel
{
    public required PagedResponse<SubjectResponse> Page { get; init; }
    public required SubjectListQuery Query { get; init; }
}

public sealed class SubjectDetailsViewModel
{
    public required SubjectResponse Subject { get; init; }
    public required PagedResponse<TopicResponse> Topics { get; init; }
    public int QuestionCount { get; init; }
}
