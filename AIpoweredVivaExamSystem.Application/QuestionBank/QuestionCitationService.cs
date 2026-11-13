namespace AIpoweredVivaExamSystem.Application.QuestionBank;

public sealed class QuestionCitationService(IQuestionBankStore store)
{
    public Task<IReadOnlyList<CitationView>> ListAsync(Guid questionId, CancellationToken ct) =>
        store.CitationsAsync(questionId, ct);
}
