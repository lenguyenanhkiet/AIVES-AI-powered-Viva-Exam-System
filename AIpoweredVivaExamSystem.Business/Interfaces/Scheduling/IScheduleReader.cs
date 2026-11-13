using AIpoweredVivaExamSystem.Business.Exams;
using AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;
using AIpoweredVivaExamSystem.DataAccess.Models.Exams;
using AIpoweredVivaExamSystem.DataAccess.Entities;

namespace AIpoweredVivaExamSystem.Business.Scheduling;

public interface IScheduleReader
{
    Task<IReadOnlyList<ExamSummary>> ListManagedAsync(CancellationToken ct);
    Task<ExamSchedule> GetAsync(Guid examId, CancellationToken ct);
    Task<IReadOnlyList<PersonalSchedule>> MyScheduleDetailsAsync(CancellationToken ct);
}
