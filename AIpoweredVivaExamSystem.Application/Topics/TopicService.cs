using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Interfaces;
using AIpoweredVivaExamSystem.Application.Rubrics.DTOs;
using AIpoweredVivaExamSystem.Application.Topics.DTOs;
using AIpoweredVivaExamSystem.Domain.Entities;
using FluentValidation;

namespace AIpoweredVivaExamSystem.Application.Topics;

public sealed class TopicService(
    ITopicRepository repository,
    IValidator<CreateTopicRequest> createValidator,
    IValidator<UpdateTopicRequest> updateValidator,
    IValidator<TopicListQuery> listValidator) : ITopicService
{
    public async Task<TopicResponse> CreateAsync(CreateTopicRequest request, CancellationToken cancellationToken)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);
        if (!await repository.SubjectExistsAsync(request.SubjectId, cancellationToken))
            throw new ResourceNotFoundException("Subject was not found.");
        var topic = new Topic(request.SubjectId, request.Name, request.Description, request.Status);
        if (await repository.NameExistsAsync(topic.SubjectId, topic.Name, null, cancellationToken))
            throw new ResourceConflictException("This subject already has a topic with the same name.");
        repository.Add(topic);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(topic);
    }

    public async Task<TopicResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Map(await FindAsync(id, cancellationToken));

    public async Task<PagedResponse<TopicResponse>> ListAsync(TopicListQuery query, CancellationToken cancellationToken)
    {
        await listValidator.ValidateAndThrowAsync(query, cancellationToken);
        var (items, totalCount) = await repository.ListAsync(
            query.SubjectId, query.Keyword?.Trim(), query.Status, query.Page, query.PageSize, cancellationToken);
        return new(items.Select(Map).ToArray(), totalCount, query.Page, query.PageSize);
    }

    public async Task<TopicResponse> UpdateAsync(Guid id, UpdateTopicRequest request, CancellationToken cancellationToken)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var topic = await FindAsync(id, cancellationToken);
        topic.Update(request.Name, request.Description, request.Status);
        if (await repository.NameExistsAsync(topic.SubjectId, topic.Name, topic.Id, cancellationToken))
            throw new ResourceConflictException("This subject already has a topic with the same name.");
        await repository.SaveChangesAsync(cancellationToken);
        return Map(topic);
    }

    // Soft delete: Questions keep their composite FK to the topic.
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var topic = await FindAsync(id, cancellationToken);
        if (await repository.HasActiveQuestionsAsync(id, cancellationToken))
            throw new ResourceConflictException("The topic still has questions. Delete or move them first.");
        topic.DeletedAt = DateTimeOffset.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task<Topic> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetAsync(id, cancellationToken)
        ?? throw new ResourceNotFoundException("Topic was not found.");

    private static TopicResponse Map(Topic topic) => new(
        topic.Id, topic.SubjectId, topic.Name, topic.Description, topic.Status,
        topic.CreatedAt, topic.UpdatedAt);
}
