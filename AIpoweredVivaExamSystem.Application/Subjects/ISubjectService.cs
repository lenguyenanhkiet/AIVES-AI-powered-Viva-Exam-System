using AIpoweredVivaExamSystem.Application.Rubrics.DTOs;
using AIpoweredVivaExamSystem.Application.Subjects.DTOs;

namespace AIpoweredVivaExamSystem.Application.Subjects;

public interface ISubjectService
{
    Task<SubjectResponse> CreateAsync(CreateSubjectRequest request, CancellationToken cancellationToken);
    Task<SubjectResponse> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<PagedResponse<SubjectResponse>> ListAsync(SubjectListQuery query, CancellationToken cancellationToken);
    Task<SubjectResponse> UpdateAsync(Guid id, UpdateSubjectRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
