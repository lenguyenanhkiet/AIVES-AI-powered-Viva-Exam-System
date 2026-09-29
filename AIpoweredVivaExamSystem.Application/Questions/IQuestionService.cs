using AIpoweredVivaExamSystem.Application.Questions.DTOs;
using AIpoweredVivaExamSystem.Application.Rubrics.DTOs;

namespace AIpoweredVivaExamSystem.Application.Questions;

public interface IQuestionService
{
    Task<QuestionResponse> CreateAsync(CreateQuestionRequest request, CancellationToken cancellationToken);
    Task<QuestionResponse> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<PagedResponse<QuestionResponse>> ListAsync(QuestionListQuery query, CancellationToken cancellationToken);
    Task<QuestionResponse> UpdateAsync(Guid id, UpdateQuestionRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<QuestionResponse> ApproveAsync(Guid id, CancellationToken cancellationToken);
    Task<QuestionResponse> RejectAsync(Guid id, CancellationToken cancellationToken);
}
