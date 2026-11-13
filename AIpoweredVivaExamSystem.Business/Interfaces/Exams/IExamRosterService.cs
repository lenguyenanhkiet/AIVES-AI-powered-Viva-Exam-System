using AIpoweredVivaExamSystem.DataAccess.Entities;

namespace AIpoweredVivaExamSystem.Business.Exams;

public interface IExamRosterService
{
    Task<IReadOnlyList<RosterEntry>> ListAsync(Guid examId, CancellationToken ct);
    Task AddAsync(Guid examId, string studentEmail, CancellationToken ct);
    Task RemoveAsync(Guid examId, Guid enrollmentId, CancellationToken ct);
}
// PreviewToken must bind exam/roster versions and intended slots; revalidate on Apply.
