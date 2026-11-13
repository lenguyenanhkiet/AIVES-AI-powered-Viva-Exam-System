using AIpoweredVivaExamSystem.DataAccess.Contracts.Interfaces;
using AIpoweredVivaExamSystem.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

namespace AIpoweredVivaExamSystem.DataAccess.Repositories;

public sealed class QuestionLookup(ApplicationDbContext context) : IQuestionLookup
{
    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        context.Questions.AnyAsync(x => x.Id == id && x.DeletedAt == null, cancellationToken);
}
