using AIpoweredVivaExamSystem.Business.Exams;
using AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;
using AIpoweredVivaExamSystem.DataAccess.Models.Exams;

namespace AIpoweredVivaExamSystem.Business.ExamStudents;

public interface IExamRosterReader
{
    Task<RosterExamPage> ListManagedAsync(int page, CancellationToken ct);
    Task<ExamSummary> GetExamAsync(Guid examId, CancellationToken ct);
}
