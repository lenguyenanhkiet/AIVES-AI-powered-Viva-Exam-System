using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Interfaces;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AIpoweredVivaExamSystem.Persistence.Repositories;

public sealed class SubjectRepository(ApplicationDbContext context) : ISubjectRepository
{
    public Task<Subject?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.Subjects.SingleOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, cancellationToken);

    public Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken) =>
        context.Subjects.AnyAsync(x => x.Code == code && x.DeletedAt == null && x.Id != excludeId, cancellationToken);

    public async Task<bool> HasActiveDependentsAsync(Guid id, CancellationToken cancellationToken) =>
        await context.Topics.AnyAsync(x => x.SubjectId == id && x.DeletedAt == null, cancellationToken)
        || await context.Questions.AnyAsync(x => x.SubjectId == id && x.DeletedAt == null, cancellationToken);

    public async Task<(IReadOnlyList<Subject> Items, int TotalCount)> ListAsync(
        string? keyword, AcademicStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = context.Subjects.AsNoTracking().Where(x => x.DeletedAt == null);
        if (!string.IsNullOrEmpty(keyword))
            query = query.Where(x => x.Code.Contains(keyword) || x.Name.Contains(keyword));
        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.Code).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public void Add(Subject subject) => context.Subjects.Add(subject);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ResourceConflictException("A subject with this code already exists.");
        }
    }
}
