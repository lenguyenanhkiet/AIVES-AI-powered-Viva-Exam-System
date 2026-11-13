using AIpoweredVivaExamSystem.DataAccess.Models.Rubrics;
using AIpoweredVivaExamSystem.DataAccess.Models.Subjects;

namespace AIpoweredVivaExamSystem.Business.Subjects;

public interface ISubjectService
{
    Task<SubjectResponse> CreateAsync(CreateSubjectRequest request, CancellationToken cancellationToken);
    Task<SubjectResponse> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<PagedResponse<SubjectResponse>> ListAsync(SubjectListQuery query, CancellationToken cancellationToken);
    Task<SubjectResponse> UpdateAsync(Guid id, UpdateSubjectRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
