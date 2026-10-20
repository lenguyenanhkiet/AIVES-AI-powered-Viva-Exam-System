using System.ComponentModel.DataAnnotations;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Domain.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AIpoweredVivaExamSystem.Web.ViewModels;

public sealed class ExamIndexViewModel
{
    public required IReadOnlyList<ExamSummary> Exams { get; init; }
    public required IReadOnlyDictionary<Guid, string> SubjectNames { get; init; }
    public required IReadOnlyDictionary<Guid, string> SubjectCodes { get; init; }
    public required Dictionary<ExamStatus, int> StatusCounts { get; init; }
    public required int TotalCount { get; init; }
    public ExamStatus? FilterStatus { get; init; }
}

public sealed class ExamFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn môn học.")]
    public Guid SubjectId { get; set; }

    [Required(ErrorMessage = "Tên kỳ thi không được để trống.")]
    [StringLength(200, ErrorMessage = "Tên kỳ thi tối đa 200 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập thời gian bắt đầu.")]
    public DateTime StartAt { get; set; } = DateTime.Today.AddDays(1).AddHours(8);

    [Required(ErrorMessage = "Vui lòng nhập thời gian kết thúc.")]
    public DateTime EndAt { get; set; } = DateTime.Today.AddDays(1).AddHours(12);

    [Range(5, 180, ErrorMessage = "Thời lượng mỗi ca từ 5 đến 180 phút.")]
    public int DurationMinutes { get; set; } = 15;

    [Range(1, 20, ErrorMessage = "Số câu hỏi chính tối thiểu từ 1 đến 20.")]
    public int NumberOfMainQuestions { get; set; } = 2;

    [Range(0, 10, ErrorMessage = "Số câu hỏi đào sâu tối đa từ 0 đến 10.")]
    public int MaxFollowUpPerQuestion { get; set; } = 1;

    public string RowVersionBase64 { get; set; } = string.Empty;

    public IReadOnlyList<SelectListItem> SubjectOptions { get; set; } = [];

    public static ExamFormViewModel From(ExamDetails details, IReadOnlyList<SelectListItem> subjects) => new()
    {
        Id = details.Summary.Id,
        SubjectId = details.Summary.SubjectId,
        Name = details.Summary.Name,
        StartAt = details.Summary.StartAt.LocalDateTime,
        EndAt = details.Summary.EndAt.LocalDateTime,
        DurationMinutes = details.DurationMinutes,
        NumberOfMainQuestions = details.MainQuestions,
        MaxFollowUpPerQuestion = details.MaxFollowUps,
        RowVersionBase64 = Convert.ToBase64String(details.RowVersion),
        SubjectOptions = subjects
    };
}

public sealed class ExamDetailsViewModel
{
    public required ExamDetails Details { get; init; }
    public required string SubjectCode { get; init; }
    public required string SubjectName { get; init; }
    public required ExamFoundationCounts Counts { get; init; }
    public bool IsPoolReady => Counts.PoolQuestions >= Details.MainQuestions;
    public bool IsRosterReady => Counts.Students > 0;
    public bool IsScheduleReady => Counts.Students > 0 && Counts.ScheduledStudents >= Counts.Students;
    public bool CanPublish => Details.Summary.Status == ExamStatus.Draft && IsPoolReady && IsRosterReady && IsScheduleReady;
    public bool CanStart => Details.Summary.Status == ExamStatus.Published && DateTimeOffset.UtcNow >= Details.Summary.StartAt && DateTimeOffset.UtcNow <= Details.Summary.EndAt;
    public bool CanComplete => Details.Summary.Status == ExamStatus.Ongoing;
    public bool CanCancel => Details.Summary.Status != ExamStatus.Completed && Details.Summary.Status != ExamStatus.Cancelled;
}
