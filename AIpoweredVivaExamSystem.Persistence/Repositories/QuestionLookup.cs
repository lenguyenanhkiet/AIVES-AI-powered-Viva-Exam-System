using AIpoweredVivaExamSystem.Application.Interfaces;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace AIpoweredVivaExamSystem.Persistence.Repositories;

public sealed class QuestionLookup(ApplicationDbContext context) : IQuestionLookup
{
    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        context.Questions.AnyAsync(x => x.Id == id && x.DeletedAt == null, cancellationToken);

    public Task<bool> IsApprovedAsync(Guid id, CancellationToken cancellationToken) =>
        context.Questions.AnyAsync(x => x.Id == id && x.DeletedAt == null &&
            x.Status == QuestionStatus.Approved, cancellationToken);
}
