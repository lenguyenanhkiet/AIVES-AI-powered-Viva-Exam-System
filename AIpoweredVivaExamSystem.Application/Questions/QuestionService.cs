using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Interfaces;
using AIpoweredVivaExamSystem.Application.Questions.DTOs;
using AIpoweredVivaExamSystem.Application.Rubrics.DTOs;
using AIpoweredVivaExamSystem.Domain.Entities;
using FluentValidation;

namespace AIpoweredVivaExamSystem.Application.Questions;

public sealed class QuestionService(
    IQuestionRepository repository,
    IValidator<CreateQuestionRequest> createValidator,
    IValidator<UpdateQuestionRequest> updateValidator,
    IValidator<QuestionListQuery> listValidator) : IQuestionService
{
    public async Task<QuestionResponse> CreateAsync(CreateQuestionRequest request, CancellationToken cancellationToken)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);
        if (!await repository.SubjectExistsAsync(request.SubjectId, cancellationToken))
            throw new ResourceNotFoundException("Subject was not found.");
        var topic = await FindTopicAsync(request.TopicId, cancellationToken);
        var question = new Question(request.SubjectId, request.Content, request.BloomLevel,
            request.Difficulty, request.ExpectedAnswer, topic);
        repository.Add(question);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(question, hasRubric: false);
    }

    public async Task<QuestionResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var question = await FindAsync(id, cancellationToken);
        return Map(question, await repository.HasRubricAsync(id, cancellationToken));
    }

    public async Task<PagedResponse<QuestionResponse>> ListAsync(QuestionListQuery query, CancellationToken cancellationToken)
    {
        await listValidator.ValidateAndThrowAsync(query, cancellationToken);
        var (items, totalCount) = await repository.ListAsync(query.SubjectId, query.TopicId, query.Keyword?.Trim(),
            query.BloomLevel, query.Difficulty, query.Status, query.Page, query.PageSize, cancellationToken);
        return new(items.Select(x => Map(x.Question, x.HasRubric)).ToArray(), totalCount, query.Page, query.PageSize);
    }

    public async Task<QuestionResponse> UpdateAsync(Guid id, UpdateQuestionRequest request, CancellationToken cancellationToken)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var question = await FindAsync(id, cancellationToken);
        var topic = await FindTopicAsync(request.TopicId, cancellationToken);
        question.Update(request.Content, request.ExpectedAnswer, request.BloomLevel, request.Difficulty, topic);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(question, await repository.HasRubricAsync(id, cancellationToken));
    }

    // Soft delete. The rubric belongs to the question, so it is removed in the same save.
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var question = await FindAsync(id, cancellationToken);
        await repository.RemoveRubricAsync(id, cancellationToken);
        question.DeletedAt = DateTimeOffset.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);
    }

    // A question is only usable for AI grading once it has a rubric.
    public async Task<QuestionResponse> ApproveAsync(Guid id, CancellationToken cancellationToken)
    {
        var question = await FindAsync(id, cancellationToken);
        if (!await repository.HasRubricAsync(id, cancellationToken))
            throw new ResourceConflictException("Add a rubric before approving this question.");
        question.Approve();
        await repository.SaveChangesAsync(cancellationToken);
        return Map(question, hasRubric: true);
    }

    public async Task<QuestionResponse> RejectAsync(Guid id, CancellationToken cancellationToken)
    {
        var question = await FindAsync(id, cancellationToken);
        question.Reject();
        await repository.SaveChangesAsync(cancellationToken);
        return Map(question, await repository.HasRubricAsync(id, cancellationToken));
    }

    private async Task<Question> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetAsync(id, cancellationToken)
        ?? throw new ResourceNotFoundException("Question was not found.");

    private async Task<Topic?> FindTopicAsync(Guid? topicId, CancellationToken cancellationToken) =>
        topicId is not { } id ? null
            : await repository.GetTopicAsync(id, cancellationToken)
              ?? throw new ResourceNotFoundException("Topic was not found.");

    private static QuestionResponse Map(Question question, bool hasRubric) => new(
        question.Id, question.SubjectId, question.TopicId, question.Content, question.ExpectedAnswer,
        question.BloomLevel, question.Difficulty, question.SourceType, question.Status, hasRubric,
        question.CreatedAt, question.UpdatedAt);
}
