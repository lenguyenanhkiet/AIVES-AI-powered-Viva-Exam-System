using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Domain.Entities;

namespace AIpoweredVivaExamSystem.Web.ViewModels.Schedules;

public sealed class ScheduleIndexViewModel
{
    public IReadOnlyList<ExamSummary> Exams { get; init; } = [];
}

public sealed class ScheduleRow
{
    public int Order { get; init; }
    public Guid EnrollmentId { get; init; }
    public string StudentName { get; init; } = "";
    public string Email { get; init; } = "";
    public DateTimeOffset? StartAt { get; init; }
    public DateTimeOffset? EndAt { get; init; }
}

public sealed class ExamScheduleViewModel
{
    public required ExamSummary Exam { get; init; }
    public int DurationMinutes { get; init; }
    public IReadOnlyList<ScheduleRow> Current { get; init; } = [];
    public IReadOnlyList<ScheduleRow>? Preview { get; init; }
    public string? PreviewToken { get; init; }
    public bool CanEdit { get; init; }
}

public sealed class MyScheduleRow
{
    public string ExamName { get; init; } = "";
    public ExamStatus Status { get; init; }
    public DateTimeOffset StartAt { get; init; }
    public DateTimeOffset EndAt { get; init; }
}

public sealed class MyScheduleViewModel
{
    public IReadOnlyList<MyScheduleRow> Items { get; init; } = [];
}
