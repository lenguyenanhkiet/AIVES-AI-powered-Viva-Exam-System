using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.QuestionBank;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AIpoweredVivaExamSystem.Persistence.Repositories;

public sealed class QuestionBankStore(ApplicationDbContext db) : IQuestionBankStore
{
    public Task<Subject?> FindSubjectAsync(string code, CancellationToken ct) => db.Subjects.SingleOrDefaultAsync(x => x.Code == code && x.DeletedAt == null, ct);
    public Task<Subject?> FindSubjectAsync(Guid id, CancellationToken ct) => db.Subjects.SingleOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
    public Task<Topic?> FindTopicAsync(Guid subjectId, string name, CancellationToken ct) => db.Topics.FirstOrDefaultAsync(x => x.SubjectId == subjectId && x.Name == name && x.DeletedAt == null, ct);
    public Task<Topic?> FindTopicAsync(Guid id, CancellationToken ct) => db.Topics.SingleOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
    public Task<int?> ImportedCountAsync(Guid userId, string hash, CancellationToken ct) => db.QuestionImportBatches
        .Where(x => x.UserId == userId && x.ContentHash == hash).Select(x => (int?)x.QuestionCount).SingleOrDefaultAsync(ct);
    public Task<CourseDocument?> FindDocumentAsync(Guid id, CancellationToken ct) => db.CourseDocuments.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<CourseDocument?> FindDocumentAsync(Guid subjectId, string hash, CancellationToken ct) => db.CourseDocuments.FirstOrDefaultAsync(x => x.SubjectId == subjectId && x.ContentHash == hash, ct);
    public async Task<IReadOnlyList<CourseDocument>> ListDocumentsAsync(CancellationToken ct) => await db.CourseDocuments.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(100).ToArrayAsync(ct);
    public async Task<IReadOnlyList<DocumentChunk>> ChunksAsync(Guid documentId, CancellationToken ct) => await db.DocumentChunks.AsNoTracking().Where(x => x.DocumentId == documentId).OrderBy(x => x.PageNumber).ThenBy(x => x.Id).ToArrayAsync(ct);
    public async Task<IReadOnlyList<CitationView>> CitationsAsync(Guid questionId, CancellationToken ct) =>
        await (from c in db.QuestionCitations join k in db.DocumentChunks on c.DocumentChunkId equals k.Id
               join d in db.CourseDocuments on k.DocumentId equals d.Id where c.QuestionId == questionId
               select new CitationView(d.FileName, k.PageNumber, c.Quote)).ToArrayAsync(ct);
    public void AddRange(IEnumerable<object> entities) => db.AddRange(entities);
    public async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { throw new ResourceConflictException("This file was already processed. Refresh the page to see the existing result."); }
    }
}
