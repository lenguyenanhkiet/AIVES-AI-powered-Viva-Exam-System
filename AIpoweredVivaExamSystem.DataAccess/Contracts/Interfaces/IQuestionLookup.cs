namespace AIpoweredVivaExamSystem.DataAccess.Contracts.Interfaces;

public interface IQuestionLookup
{
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> IsApprovedAsync(Guid id, CancellationToken cancellationToken);
}
