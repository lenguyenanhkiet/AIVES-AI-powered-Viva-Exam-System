using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Interfaces;
using AIpoweredVivaExamSystem.Application.Rubrics.DTOs;
using AIpoweredVivaExamSystem.Application.Subjects.DTOs;
using AIpoweredVivaExamSystem.Domain.Entities;
using FluentValidation;

namespace AIpoweredVivaExamSystem.Application.Subjects;

public sealed class SubjectService(
    ISubjectRepository repository,
    IValidator<CreateSubjectRequest> createValidator,
    IValidator<UpdateSubjectRequest> updateValidator,
    IValidator<SubjectListQuery> listValidator) : ISubjectService
{
    public async Task<SubjectResponse> CreateAsync(CreateSubjectRequest request, CancellationToken cancellationToken)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var subject = new Subject(request.Code, request.Name, request.Description, request.Status);
        if (await repository.CodeExistsAsync(subject.Code, null, cancellationToken))
            throw new ResourceConflictException("A subject with this code already exists.");
        repository.Add(subject);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(subject);
    }

    public async Task<SubjectResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Map(await FindAsync(id, cancellationToken));

    public async Task<PagedResponse<SubjectResponse>> ListAsync(SubjectListQuery query, CancellationToken cancellationToken)
    {
        await listValidator.ValidateAndThrowAsync(query, cancellationToken);
        var (items, totalCount) = await repository.ListAsync(
            query.Keyword?.Trim(), query.Status, query.Page, query.PageSize, cancellationToken);
        return new(items.Select(Map).ToArray(), totalCount, query.Page, query.PageSize);
    }

    public async Task<SubjectResponse> UpdateAsync(Guid id, UpdateSubjectRequest request, CancellationToken cancellationToken)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var subject = await FindAsync(id, cancellationToken);
        subject.Update(request.Code, request.Name, request.Description, request.Status);
        if (await repository.CodeExistsAsync(subject.Code, subject.Id, cancellationToken))
            throw new ResourceConflictException("A subject with this code already exists.");
        await repository.SaveChangesAsync(cancellationToken);
        return Map(subject);
    }

    // Soft delete: Questions and Topics keep their FK to the subject.
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var subject = await FindAsync(id, cancellationToken);
        if (await repository.HasActiveDependentsAsync(id, cancellationToken))
            throw new ResourceConflictException("The subject still has topics or questions. Delete them first.");
        subject.DeletedAt = DateTimeOffset.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task<Subject> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetAsync(id, cancellationToken)
        ?? throw new ResourceNotFoundException("Subject was not found.");

    private static SubjectResponse Map(Subject subject) => new(
        subject.Id, subject.Code, subject.Name, subject.Description, subject.Status,
        subject.CreatedAt, subject.UpdatedAt);
}
