using AIpoweredVivaExamSystem.Application.Rubrics.DTOs;
using AIpoweredVivaExamSystem.Application.Topics.DTOs;

namespace AIpoweredVivaExamSystem.Application.Topics;

public interface ITopicService
{
    Task<TopicResponse> CreateAsync(CreateTopicRequest request, CancellationToken cancellationToken);
    Task<TopicResponse> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<PagedResponse<TopicResponse>> ListAsync(TopicListQuery query, CancellationToken cancellationToken);
    Task<TopicResponse> UpdateAsync(Guid id, UpdateTopicRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
