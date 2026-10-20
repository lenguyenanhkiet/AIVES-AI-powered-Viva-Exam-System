using System.ComponentModel.DataAnnotations;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Domain.Entities;

namespace AIpoweredVivaExamSystem.Web.ViewModels.ExamStudents;

// Only Email is bound from the form. Exam/enrollment IDs come from the route and are checked by the service.
public sealed class AddRosterStudentViewModel
{
    private string email = "";
    [Required(ErrorMessage = "Vui lòng nhập email sinh viên.")]
    [EmailAddress(ErrorMessage = "Email sinh viên không hợp lệ.")]
    [StringLength(255)]
    public string Email { get => email; set => email = value?.Trim() ?? ""; }
}

public sealed class ExamRosterViewModel
{
    public required ExamSummary Exam { get; init; }
    public required IReadOnlyList<RosterEntry> Students { get; init; }
    public AddRosterStudentViewModel Form { get; init; } = new();
    public bool CanEdit => Exam.Status == ExamStatus.Draft;
}
