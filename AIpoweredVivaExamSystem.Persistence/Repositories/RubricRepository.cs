using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Interfaces;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AIpoweredVivaExamSystem.Persistence.Repositories;

public sealed class RubricRepository(ApplicationDbContext context) : IRubricRepository
{
    public Task<Rubric?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.Rubrics.Include(x => x.Criteria).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> ExistsForQuestionAsync(Guid questionId, CancellationToken cancellationToken) =>
        context.Rubrics.AnyAsync(x => x.QuestionId == questionId, cancellationToken);

    public async Task<(IReadOnlyList<Rubric> Items, int TotalCount)> ListAsync(
        Guid? questionId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = context.Rubrics.AsNoTracking();
        if (questionId.HasValue)
            query = query.Where(x => x.QuestionId == questionId.Value);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).Include(x => x.Criteria)
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public void Add(Rubric rubric) => context.Rubrics.Add(rubric);

    public void MarkUpdated(Rubric rubric) =>
        context.Entry(rubric).Property(x => x.Name).IsModified = true;

    public void Remove(Rubric rubric) => context.Rubrics.Remove(rubric);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ResourceConflictException("The rubric changed during this operation. Reload it and try again.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ResourceConflictException("This question already has a rubric.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 547 })
        {
            throw new ResourceConflictException("The operation conflicts with related data. Reload and check the question or criterion references.");
        }
    }
}
