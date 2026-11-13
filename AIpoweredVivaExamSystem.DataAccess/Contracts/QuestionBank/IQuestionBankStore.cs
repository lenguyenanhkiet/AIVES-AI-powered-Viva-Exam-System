using AIpoweredVivaExamSystem.DataAccess.Entities;
using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Contracts.QuestionBank;

public interface IQuestionBankStore
{
    Task<Subject?> FindSubjectAsync(string code, CancellationToken ct);
    Task<Subject?> FindSubjectAsync(Guid id, CancellationToken ct);
    Task<Topic?> FindTopicAsync(Guid subjectId, string name, CancellationToken ct);
    Task<Topic?> FindTopicAsync(Guid id, CancellationToken ct);
    Task<int?> ImportedCountAsync(Guid userId, string hash, CancellationToken ct);
    Task<CourseDocument?> FindDocumentAsync(Guid id, CancellationToken ct);
    Task<CourseDocument?> FindDocumentAsync(Guid subjectId, string hash, CancellationToken ct);
    Task<IReadOnlyList<CourseDocument>> ListDocumentsAsync(CancellationToken ct);
    Task<IReadOnlyList<DocumentChunk>> ChunksAsync(Guid documentId, CancellationToken ct);
    Task<IReadOnlyList<CitationView>> CitationsAsync(Guid questionId, CancellationToken ct);
    void AddRange(IEnumerable<object> entities);
    Task SaveAsync(CancellationToken ct);
}
