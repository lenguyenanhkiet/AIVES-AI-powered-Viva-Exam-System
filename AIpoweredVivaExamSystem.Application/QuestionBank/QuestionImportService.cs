using System.Security.Cryptography;
using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;

namespace AIpoweredVivaExamSystem.Application.QuestionBank;

public sealed class QuestionImportService(IQuestionFileReader reader, IQuestionBankStore store, ICurrentUser actor)
{
    public async Task<ImportPreview> PreviewAsync(byte[] content, string fileName, CancellationToken ct)
        => (await PrepareAsync(content, fileName, ct)).Preview;

    public async Task<int> ImportAsync(byte[] content, string fileName, CancellationToken ct)
    {
        RequireLecturer();
        var hash = Convert.ToHexString(SHA256.HashData(content));
        if (await store.ImportedCountAsync(actor.Id, hash, ct) is { } imported) return imported;
        var (preview, questions) = await PrepareAsync(content, fileName, ct);
        if (preview.Errors.Count > 0 || questions.Count == 0)
            throw new ResourceConflictException("Fix every row error before importing.");
        store.AddRange(questions);
        store.AddRange([new QuestionImportBatch(actor.Id, hash, questions.Count)]);
        // One SaveChanges transaction persists all rows and the idempotency receipt atomically.
        await store.SaveAsync(ct);
        return questions.Count;
    }

    private async Task<(ImportPreview Preview, List<Question> Questions)> PrepareAsync(byte[] content, string fileName, CancellationToken ct)
    {
        RequireLecturer();
        if (content.Length is 0 or > 5 * 1024 * 1024)
            throw new ResourceConflictException("Import files must be between 1 byte and 5 MB.");
        var rows = reader.Read(content, fileName);
        if (rows.Count is 0 or > 1000) throw new ResourceConflictException("Import requires 1 to 1000 rows.");
        var errors = new List<ImportError>();
        var questions = new List<Question>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            var subject = await store.FindSubjectAsync(row.SubjectCode.Trim(), ct);
            var topic = string.IsNullOrWhiteSpace(row.Topic) || subject is null ? null
                : await store.FindTopicAsync(subject.Id, row.Topic.Trim(), ct);
            var issues = new List<string>();
            if (subject is null || subject.Status != AcademicStatus.Active) issues.Add("Subject is unavailable or not assigned to you.");
            if (!string.IsNullOrWhiteSpace(row.Topic) && topic is null) issues.Add("Topic does not belong to this subject.");
            if (string.IsNullOrWhiteSpace(row.Content) || row.Content.Length > 4000) issues.Add("Content is required (max 4000 characters).");
            if (row.ExpectedAnswer.Length > 8000) issues.Add("ExpectedAnswer exceeds 8000 characters.");
            if (!Enum.TryParse<BloomLevel>(row.BloomLevel, true, out var bloom) || !Enum.IsDefined(bloom)) issues.Add("Invalid BloomLevel.");
            if (!Enum.TryParse<QuestionDifficulty>(row.Difficulty, true, out var difficulty) || !Enum.IsDefined(difficulty)) issues.Add("Invalid Difficulty.");
            if (!seen.Add(row.SubjectCode.Trim() + "\n" + row.Content.Trim())) issues.Add("Duplicate question within file.");
            if (issues.Count > 0) errors.Add(new(row.RowNumber, string.Join(" ", issues)));
            else questions.Add(new Question(subject!.Id, row.Content, bloom, difficulty, row.ExpectedAnswer, topic, QuestionSourceType.Imported));
        }
        return (new(questions.Count, rows, errors), questions);
    }

    private void RequireLecturer()
    {
        if (actor.Id == Guid.Empty || (!actor.IsAdministrator && !actor.IsLecturer))
            throw new UnauthorizedAccessException("Lecturer access is required.");
    }
}
