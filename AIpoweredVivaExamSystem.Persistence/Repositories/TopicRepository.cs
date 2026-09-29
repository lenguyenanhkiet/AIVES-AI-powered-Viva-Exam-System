using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Interfaces;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AIpoweredVivaExamSystem.Persistence.Repositories;

public sealed class TopicRepository(ApplicationDbContext context) : ITopicRepository
{
    public Task<Topic?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.Topics.SingleOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, cancellationToken);

    public Task<bool> SubjectExistsAsync(Guid subjectId, CancellationToken cancellationToken) =>
        context.Subjects.AnyAsync(x => x.Id == subjectId && x.DeletedAt == null, cancellationToken);

    public Task<bool> NameExistsAsync(Guid subjectId, string name, Guid? excludeId, CancellationToken cancellationToken) =>
        context.Topics.AnyAsync(x => x.SubjectId == subjectId && x.Name == name
            && x.DeletedAt == null && x.Id != excludeId, cancellationToken);

    public Task<bool> HasActiveQuestionsAsync(Guid id, CancellationToken cancellationToken) =>
        context.Questions.AnyAsync(x => x.TopicId == id && x.DeletedAt == null, cancellationToken);

    public async Task<(IReadOnlyList<Topic> Items, int TotalCount)> ListAsync(
        Guid? subjectId, string? keyword, AcademicStatus? status, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var query = context.Topics.AsNoTracking().Where(x => x.DeletedAt == null);
        if (subjectId.HasValue)
            query = query.Where(x => x.SubjectId == subjectId.Value);
        if (!string.IsNullOrEmpty(keyword))
            query = query.Where(x => x.Name.Contains(keyword));
        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public void Add(Topic topic) => context.Topics.Add(topic);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 547 })
        {
            throw new ResourceConflictException("The subject was removed during this operation. Reload and try again.");
        }
    }
}
