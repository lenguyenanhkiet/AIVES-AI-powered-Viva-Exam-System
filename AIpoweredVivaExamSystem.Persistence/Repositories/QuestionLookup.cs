using AIpoweredVivaExamSystem.Application.Interfaces;
using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace AIpoweredVivaExamSystem.Persistence.Repositories;

public sealed class QuestionLookup(ApplicationDbContext context) : IQuestionLookup
{
    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        context.Questions.AnyAsync(x => x.Id == id && x.DeletedAt == null, cancellationToken);
}
