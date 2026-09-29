using System.ComponentModel.DataAnnotations;
using AIpoweredVivaExamSystem.Application.Questions.DTOs;
using AIpoweredVivaExamSystem.Application.Rubrics.DTOs;
using AIpoweredVivaExamSystem.Application.Subjects.DTOs;
using AIpoweredVivaExamSystem.Application.Topics.DTOs;
using AIpoweredVivaExamSystem.Domain.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AIpoweredVivaExamSystem.Web.Models;

public sealed class QuestionFormViewModel
{
    public Guid? Id { get; set; }

    [Display(Name = "Môn học")]
    [Required(ErrorMessage = "Vui lòng chọn môn học.")]
    public Guid? SubjectId { get; set; }

    [Display(Name = "Chủ đề")]
    public Guid? TopicId { get; set; }

    [Display(Name = "Nội dung câu hỏi")]
    [Required(ErrorMessage = "Vui lòng nhập nội dung câu hỏi.")]
    [StringLength(4000, ErrorMessage = "Nội dung câu hỏi tối đa 4000 ký tự.")]
    public string Content { get; set; } = string.Empty;

    [Display(Name = "Đáp án mong đợi")]
    [StringLength(8000, ErrorMessage = "Đáp án mong đợi tối đa 8000 ký tự.")]
    public string? ExpectedAnswer { get; set; }

    [Display(Name = "Mức Bloom")]
    [Required(ErrorMessage = "Vui lòng chọn mức Bloom.")]
    public BloomLevel? BloomLevel { get; set; }

    [Display(Name = "Độ khó")]
    [Required(ErrorMessage = "Vui lòng chọn độ khó.")]
    public QuestionDifficulty? Difficulty { get; set; }

    // Shown on the edit form; always reloaded from the database, never posted.
    [BindNever]
    public QuestionStatus? CurrentStatus { get; set; }

    public static QuestionFormViewModel From(QuestionResponse question) => new()
    {
        Id = question.Id, SubjectId = question.SubjectId, TopicId = question.TopicId,
        Content = question.Content, ExpectedAnswer = question.ExpectedAnswer,
        BloomLevel = question.BloomLevel, Difficulty = question.Difficulty, CurrentStatus = question.Status
    };
}

public sealed class QuestionIndexViewModel
{
    public required PagedResponse<QuestionResponse> Page { get; init; }
    public required QuestionListQuery Query { get; init; }
    public required IReadOnlyDictionary<Guid, SubjectResponse> Subjects { get; init; }
    public required IReadOnlyDictionary<Guid, TopicResponse> Topics { get; init; }
    // Count per status tab for the current filters; AllCount ignores the status filter.
    public required IReadOnlyDictionary<QuestionStatus, int> StatusCounts { get; init; }
    public required int AllCount { get; init; }
}

public sealed class QuestionDetailsViewModel
{
    public required QuestionResponse Question { get; init; }
    public required SubjectResponse Subject { get; init; }
    public TopicResponse? Topic { get; init; }
    public RubricResponse? Rubric { get; init; }
}

public sealed class RubricFormViewModel
{
    public Guid? Id { get; set; }
    public Guid QuestionId { get; set; }

    [Display(Name = "Tên rubric")]
    [Required(ErrorMessage = "Vui lòng nhập tên rubric.")]
    [StringLength(255, ErrorMessage = "Tên rubric tối đa 255 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    public List<CriterionFormViewModel> Criteria { get; set; } = [];

    public static RubricFormViewModel From(RubricResponse rubric) => new()
    {
        Id = rubric.Id, QuestionId = rubric.QuestionId, Name = rubric.Name, Description = rubric.Description,
        Criteria = rubric.Criteria.Select(x => new CriterionFormViewModel
        {
            Id = x.Id, Name = x.Name, Description = x.Description,
            ExpectedConcepts = x.ExpectedConcepts, MaxScore = x.MaxScore
        }).ToList()
    };
}

public sealed class CriterionFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Nhập tên tiêu chí.")]
    [StringLength(255, ErrorMessage = "Tên tiêu chí tối đa 255 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập điểm tối đa.")]
    [Range(typeof(decimal), "0.01", "999.99", ErrorMessage = "Điểm từ 0.01 đến 999.99.")]
    public decimal? MaxScore { get; set; }

    public string? Description { get; set; }

    public string? ExpectedConcepts { get; set; }
}
