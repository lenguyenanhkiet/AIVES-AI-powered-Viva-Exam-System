using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Interfaces;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AIpoweredVivaExamSystem.Persistence.Repositories;

public sealed class QuestionRepository(ApplicationDbContext context) : IQuestionRepository
{
    public Task<Question?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.Questions.SingleOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, cancellationToken);

    public Task<bool> SubjectExistsAsync(Guid subjectId, CancellationToken cancellationToken) =>
        context.Subjects.AnyAsync(x => x.Id == subjectId && x.DeletedAt == null, cancellationToken);

    public Task<Topic?> GetTopicAsync(Guid topicId, CancellationToken cancellationToken) =>
        context.Topics.AsNoTracking().SingleOrDefaultAsync(x => x.Id == topicId && x.DeletedAt == null, cancellationToken);

    public Task<bool> HasRubricAsync(Guid id, CancellationToken cancellationToken) =>
        context.Rubrics.AnyAsync(x => x.QuestionId == id, cancellationToken);

    public async Task RemoveRubricAsync(Guid id, CancellationToken cancellationToken)
    {
        var rubric = await context.Rubrics.Include(x => x.Criteria)
            .SingleOrDefaultAsync(x => x.QuestionId == id, cancellationToken);
        if (rubric is not null)
            context.Rubrics.Remove(rubric);
    }

    public async Task<(IReadOnlyList<(Question Question, bool HasRubric)> Items, int TotalCount)> ListAsync(
        Guid? subjectId, Guid? topicId, string? keyword, BloomLevel? bloomLevel,
        QuestionDifficulty? difficulty, QuestionStatus? status, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var query = context.Questions.AsNoTracking().Where(x => x.DeletedAt == null);
        if (subjectId.HasValue)
            query = query.Where(x => x.SubjectId == subjectId.Value);
        if (topicId.HasValue)
            query = query.Where(x => x.TopicId == topicId.Value);
        if (!string.IsNullOrEmpty(keyword))
            query = query.Where(x => x.Content.Contains(keyword));
        if (bloomLevel.HasValue)
            query = query.Where(x => x.BloomLevel == bloomLevel.Value);
        if (difficulty.HasValue)
            query = query.Where(x => x.Difficulty == difficulty.Value);
        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { Question = x, HasRubric = context.Rubrics.Any(r => r.QuestionId == x.Id) })
            .ToListAsync(cancellationToken);
        return (items.Select(x => (x.Question, x.HasRubric)).ToArray(), totalCount);
    }

    public void Add(Question question) => context.Questions.Add(question);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 547 })
        {
            throw new ResourceConflictException("The subject or topic was removed during this operation. Reload and try again.");
        }
    }
}
