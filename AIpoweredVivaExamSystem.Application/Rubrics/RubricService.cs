using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Interfaces;
using AIpoweredVivaExamSystem.Application.Rubrics.DTOs;
using AIpoweredVivaExamSystem.Domain.Entities;
using FluentValidation;

namespace AIpoweredVivaExamSystem.Application.Rubrics;

public sealed class RubricService(
    IRubricRepository repository, IQuestionLookup questions,
    IValidator<CreateRubricRequest> createValidator,
    IValidator<UpdateRubricRequest> updateValidator,
    IValidator<RubricListQuery> listValidator) : IRubricService
{
    public async Task<RubricResponse> CreateAsync(CreateRubricRequest request, CancellationToken cancellationToken)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);
        if (!await questions.ExistsAsync(request.QuestionId, cancellationToken))
            throw new ResourceNotFoundException("Question was not found.");
        if (await repository.ExistsForQuestionAsync(request.QuestionId, cancellationToken))
            throw new ResourceConflictException("This question already has a rubric.");
        var rubric = new Rubric(request.QuestionId, request.Name, request.Description, Definitions(request.Criteria));
        repository.Add(rubric);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(rubric);
    }

    public async Task<RubricResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Map(await FindAsync(id, cancellationToken));

    public async Task<PagedResponse<RubricResponse>> ListAsync(RubricListQuery query, CancellationToken cancellationToken)
    {
        await listValidator.ValidateAndThrowAsync(query, cancellationToken);
        var (items, totalCount) = await repository.ListAsync(query.QuestionId, query.Page, query.PageSize, cancellationToken);
        return new(items.Select(Map).ToArray(), totalCount, query.Page, query.PageSize);
    }

    public async Task<RubricResponse> UpdateAsync(Guid id, UpdateRubricRequest request, CancellationToken cancellationToken)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var rubric = await FindAsync(id, cancellationToken);
        rubric.Update(request.Name, request.Description, Definitions(request.Criteria));
        repository.MarkUpdated(rubric);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(rubric);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var rubric = await FindAsync(id, cancellationToken);
        repository.Remove(rubric);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task<Rubric> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetAsync(id, cancellationToken)
        ?? throw new ResourceNotFoundException("Rubric was not found.");

    private static CriterionDefinition[] Definitions(IEnumerable<CriterionRequest> criteria) =>
        criteria.Select(x => new CriterionDefinition(x.Id, x.Name, x.Description, x.ExpectedConcepts,
            x.MaxScore, x.DisplayOrder)).ToArray();

    private static RubricResponse Map(Rubric rubric) => new(
        rubric.Id, rubric.QuestionId, rubric.Name, rubric.Description, rubric.TotalScore,
        rubric.CreatedAt, rubric.UpdatedAt,
        rubric.Criteria.OrderBy(x => x.DisplayOrder).Select(x => new RubricCriterionResponse(
            x.Id, x.Name, x.Description, x.ExpectedConcepts, x.MaxScore, x.DisplayOrder)).ToArray());
}
