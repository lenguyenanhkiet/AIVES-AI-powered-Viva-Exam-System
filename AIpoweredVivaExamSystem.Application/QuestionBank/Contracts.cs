using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;

namespace AIpoweredVivaExamSystem.Application.QuestionBank;

public sealed record ImportRow(int RowNumber, string SubjectCode, string Topic, string Content,
    string ExpectedAnswer, string BloomLevel, string Difficulty);
public sealed record ImportError(int RowNumber, string Message);
public sealed record ImportPreview(int ValidCount, IReadOnlyList<ImportRow> Rows, IReadOnlyList<ImportError> Errors);
public interface IQuestionFileReader
{
    IReadOnlyList<ImportRow> Read(byte[] content, string fileName);
}
public sealed record ExtractedPage(int Number, string Text);
public interface IDocumentTextExtractor
{
    IReadOnlyList<ExtractedPage> Extract(byte[] content, string fileName);
}
public sealed record GeneratedDraft(string Content, string ExpectedAnswer, Guid ChunkId, string Quote);
public interface IQuestionGenerator
{
    Task<IReadOnlyList<GeneratedDraft>> GenerateAsync(string instruction, BloomLevel bloom,
        QuestionDifficulty difficulty, int count, IReadOnlyList<DocumentChunk> sources, CancellationToken ct);
}
public sealed record CitationView(string FileName, int PageNumber, string Quote);
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
