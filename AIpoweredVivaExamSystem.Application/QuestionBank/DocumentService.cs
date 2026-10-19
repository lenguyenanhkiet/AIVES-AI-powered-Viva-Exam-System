using System.Security.Cryptography;
using System.Text.RegularExpressions;
using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;

namespace AIpoweredVivaExamSystem.Application.QuestionBank;

public sealed class DocumentService(IQuestionBankStore store, IDocumentTextExtractor extractor,
    IQuestionGenerator generator, ICurrentUser actor)
{
    public Task<IReadOnlyList<CourseDocument>> ListAsync(CancellationToken ct)
    { RequireLecturer(); return store.ListDocumentsAsync(ct); }

    public async Task<CourseDocument> UploadAsync(Guid subjectId, byte[] content, string fileName, CancellationToken ct)
    {
        RequireLecturer();
        if (content.Length is 0 or > 20 * 1024 * 1024) throw new ResourceConflictException("Documents must be 1 byte to 20 MB.");
        if (await store.FindSubjectAsync(subjectId, ct) is not { Status: AcademicStatus.Active })
            throw new ResourceNotFoundException("Subject is unavailable or not assigned to you.");
        var hash = Convert.ToHexString(SHA256.HashData(content));
        if (await store.FindDocumentAsync(subjectId, hash, ct) is { } existing) return existing;
        var pages = extractor.Extract(content, fileName);
        var document = new CourseDocument(subjectId, Path.GetFileName(fileName), hash);
        var chunks = pages.SelectMany(page => Chunk(page.Text).Select(text => new DocumentChunk(document.Id, page.Number, text))).ToArray();
        if (chunks.Length == 0) throw new ResourceConflictException("No readable text found. Scanned PDFs require OCR before upload.");
        if (chunks.Length > 2000) throw new ResourceConflictException("Document exceeds the 2000-chunk limit.");
        store.AddRange([document]); store.AddRange(chunks);
        await store.SaveAsync(ct);
        return document;
    }

    public async Task<IReadOnlyList<Guid>> GenerateAsync(Guid documentId, Guid? topicId, string instruction,
        BloomLevel bloom, QuestionDifficulty difficulty, int count, CancellationToken ct)
    {
        RequireLecturer();
        if (count is < 1 or > 10 || instruction.Length is < 3 or > 1000 || !Enum.IsDefined(bloom) || !Enum.IsDefined(difficulty))
            throw new ResourceConflictException("Choose 1–10 questions, valid Bloom/difficulty and a 3–1000 character instruction.");
        var document = await store.FindDocumentAsync(documentId, ct) ?? throw new ResourceNotFoundException("Document not found.");
        if (await store.FindSubjectAsync(document.SubjectId, ct) is not { Status: AcademicStatus.Active })
            throw new ResourceConflictException("The document's subject is no longer active.");
        var topic = topicId is { } id ? await store.FindTopicAsync(id, ct) : null;
        if (topicId.HasValue && (topic is null || topic.SubjectId != document.SubjectId))
            throw new ResourceConflictException("Topic must belong to the document's subject.");
        var chunks = await store.ChunksAsync(documentId, ct);
        var sources = Retrieve(chunks, instruction, 8);
        if (sources.Count == 0) throw new ResourceConflictException("No document sources available.");
        var drafts = await generator.GenerateAsync(instruction, bloom, difficulty, count, sources, ct);
        if (drafts.Count != count) throw new ResourceConflictException("AI returned an unexpected question count. No questions were saved.");
        var entities = new List<object>();
        var ids = new List<Guid>();
        foreach (var draft in drafts)
        {
            var chunk = sources.SingleOrDefault(x => x.Id == draft.ChunkId)
                ?? throw new ResourceConflictException("AI cited an unknown source. No questions were saved.");
            if (draft.Content.Length > 4000 || draft.ExpectedAnswer.Length > 8000)
                throw new ResourceConflictException("AI response exceeded question limits.");
            var question = new Question(document.SubjectId, draft.Content, bloom, difficulty,
                draft.ExpectedAnswer, topic, QuestionSourceType.DocumentGenerated);
            entities.Add(question);
            entities.Add(new QuestionCitation(question.Id, chunk, draft.Quote));
            ids.Add(question.Id);
        }
        store.AddRange(entities); await store.SaveAsync(ct);
        return ids;
    }

    // Lexical retrieval is deterministic, local and requires no embedding service or vector database.
    public static IReadOnlyList<DocumentChunk> Retrieve(IReadOnlyList<DocumentChunk> chunks, string query, int count)
    {
        var terms = Regex.Matches(query.ToLowerInvariant(), @"[\p{L}\p{N}]{2,}")
            .Select(x => x.Value).Distinct().ToArray();
        return chunks.OrderByDescending(c => terms.Count(t => c.Text.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .ThenBy(c => c.PageNumber).ThenBy(c => c.Id).Take(count).ToArray();
    }

    public static IEnumerable<string> Chunk(string text)
    {
        text = Regex.Replace(text, @"\s+", " ").Trim();
        const int size = 2000, overlap = 200;
        for (var offset = 0; offset < text.Length; offset += size - overlap)
        {
            yield return text.Substring(offset, Math.Min(size, text.Length - offset));
            if (offset + size >= text.Length) break;
        }
    }
    private void RequireLecturer()
    {
        if (actor.Id == Guid.Empty || (!actor.IsAdministrator && !actor.IsLecturer))
            throw new UnauthorizedAccessException("Lecturer access is required.");
    }
}
