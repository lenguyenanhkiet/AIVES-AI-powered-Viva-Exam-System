using AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;
using AIpoweredVivaExamSystem.DataAccess.Models.Exams;
using AIpoweredVivaExamSystem.DataAccess.Entities;

namespace AIpoweredVivaExamSystem.DataAccess.Contracts.Scheduling;

public interface IExamScheduleStore
{
    Task<Exam?> FindExamAsync(Guid examId, CancellationToken ct);
    Task<IReadOnlyList<ScheduleEnrollment>> ListRosterAsync(Guid examId, CancellationToken ct);
    Task LockSchedulingAsync(CancellationToken ct);
    Task<Exam> LockExamAsync(Guid examId, CancellationToken ct);
    Task ValidateAndStageAsync(Exam exam, IReadOnlyList<ScheduleSlot> slots, CancellationToken ct);
    Task<IReadOnlyList<PersonalSchedule>> MyScheduleAsync(Guid studentId, CancellationToken ct);
}
