using AIpoweredVivaExamSystem.DataAccess.Models.Rubrics;
using AIpoweredVivaExamSystem.DataAccess.Models.Topics;

namespace AIpoweredVivaExamSystem.Business.Topics;

public interface ITopicService
{
    Task<TopicResponse> CreateAsync(CreateTopicRequest request, CancellationToken cancellationToken);
    Task<TopicResponse> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<PagedResponse<TopicResponse>> ListAsync(TopicListQuery query, CancellationToken cancellationToken);
    Task<TopicResponse> UpdateAsync(Guid id, UpdateTopicRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
