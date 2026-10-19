using AIpoweredVivaExamSystem.Domain.Common;

namespace AIpoweredVivaExamSystem.Domain.Entities;

public sealed class CourseDocument : AuditableEntity
{
    public Guid SubjectId { get; private set; }
    public string FileName { get; private set; } = "";
    public string ContentHash { get; private set; } = "";
    private CourseDocument() { }
    public CourseDocument(Guid subjectId, string fileName, string contentHash)
    {
        DomainRules.RequiredId(subjectId, nameof(SubjectId));
        SubjectId = subjectId;
        FileName = DomainRules.RequiredText(fileName, nameof(FileName), 255);
        ContentHash = DomainRules.RequiredText(contentHash, nameof(ContentHash), 64);
    }
}

public sealed class DocumentChunk : BaseEntity
{
    public Guid DocumentId { get; private set; }
    public int PageNumber { get; private set; }
    public string Text { get; private set; } = "";
    private DocumentChunk() { }
    public DocumentChunk(Guid documentId, int pageNumber, string text)
    {
        DomainRules.RequiredId(documentId, nameof(DocumentId));
        if (pageNumber < 1) throw new DomainValidationException("Page/slide must be positive.");
        DocumentId = documentId; PageNumber = pageNumber;
        Text = DomainRules.RequiredText(text, nameof(Text), 2400);
    }
}

public sealed class QuestionCitation : BaseEntity
{
    public Guid QuestionId { get; private set; }
    public Guid DocumentChunkId { get; private set; }
    public string Quote { get; private set; } = "";
    private QuestionCitation() { }
    public QuestionCitation(Guid questionId, DocumentChunk chunk, string quote)
    {
        DomainRules.RequiredId(questionId, nameof(QuestionId));
        var valid = DomainRules.RequiredText(quote, nameof(Quote), 2400);
        if (!chunk.Text.Contains(valid, StringComparison.Ordinal))
            throw new DomainValidationException("Citation must quote the retrieved source exactly.");
        QuestionId = questionId; DocumentChunkId = chunk.Id; Quote = valid;
    }
}

public sealed class QuestionImportBatch : BaseEntity
{
    public Guid UserId { get; private set; }
    public string ContentHash { get; private set; } = "";
    public int QuestionCount { get; private set; }
    private QuestionImportBatch() { }
    public QuestionImportBatch(Guid userId, string hash, int count)
    {
        UserId = userId; ContentHash = hash; QuestionCount = count;
    }
}
