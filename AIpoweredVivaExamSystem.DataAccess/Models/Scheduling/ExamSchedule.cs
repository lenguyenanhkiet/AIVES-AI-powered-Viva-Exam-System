using AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;
using AIpoweredVivaExamSystem.DataAccess.Models.Exams;
using AIpoweredVivaExamSystem.DataAccess.Entities;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Scheduling;

public sealed record ExamSchedule(ExamSummary Exam, int DurationMinutes, IReadOnlyList<ScheduleEnrollment> Roster);
