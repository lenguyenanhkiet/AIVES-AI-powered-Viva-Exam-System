namespace AIpoweredVivaExamSystem.Business.QuestionBank;

public sealed class QuestionBankReadService(IQuestionBankStore store, ICurrentUser actor)
{
    public Task<CourseDocument?> FindDocumentAsync(Guid documentId, CancellationToken ct)
    {
        RequireManager();
        return store.FindDocumentAsync(documentId, ct);
    }

    public Task<IReadOnlyList<CitationView>> CitationsAsync(Guid questionId, CancellationToken ct)
    {
        RequireManager();
        return store.CitationsAsync(questionId, ct);
    }

    private void RequireManager()
    {
        if (!actor.IsAdministrator && !actor.IsLecturer) throw new UnauthorizedAccessException("Manager access is required.");
    }
}
