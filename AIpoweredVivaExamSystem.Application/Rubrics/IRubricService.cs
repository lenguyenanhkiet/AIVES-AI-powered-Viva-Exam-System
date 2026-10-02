using AIpoweredVivaExamSystem.Application.Rubrics.DTOs;

namespace AIpoweredVivaExamSystem.Application.Rubrics;

public interface IRubricService
{
    Task<RubricResponse> CreateAsync(CreateRubricRequest request, CancellationToken cancellationToken);
    Task<RubricResponse> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<RubricResponse?> GetByQuestionIdAsync(Guid questionId, CancellationToken cancellationToken);
    Task<PagedResponse<RubricResponse>> ListAsync(RubricListQuery query, CancellationToken cancellationToken);
    Task<RubricResponse> UpdateAsync(Guid id, UpdateRubricRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
