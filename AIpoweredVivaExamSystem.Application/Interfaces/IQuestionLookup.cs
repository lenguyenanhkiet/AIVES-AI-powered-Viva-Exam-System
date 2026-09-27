namespace AIpoweredVivaExamSystem.Application.Interfaces;

public interface IQuestionLookup
{
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);
}
