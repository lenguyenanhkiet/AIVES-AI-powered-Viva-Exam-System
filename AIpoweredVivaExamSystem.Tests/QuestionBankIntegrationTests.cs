using System.Text;
using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.QuestionBank;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Infrastructure.QuestionBank;
using AIpoweredVivaExamSystem.Persistence.Context;
using AIpoweredVivaExamSystem.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AIpoweredVivaExamSystem.Tests;

[Collection("SQL Server")]
public class QuestionBankIntegrationTests(SqlServerFixture fixture)
{
    private sealed record Actor(Guid Id, bool IsAdministrator = false, bool IsLecturer = true) : ICurrentUser;
    private ApplicationDbContext Scoped(ICurrentUser actor) => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlServer(fixture.ConnectionString).Options, actor);
    private async Task<(Actor Actor, Subject Subject)> SeedAsync()
    {
        await using var db = fixture.CreateContext();
        var user = new User(Guid.NewGuid()+"@example.com", "unused", "Lecturer", UserStatus.Active);
        user.AssignRole(UserRole.LECTURER);
        var subject = new Subject("S"+Guid.NewGuid().ToString("N"), "Subject");
        db.AddRange(user, subject, new LecturerSubject(user.Id, subject.Id));
        await db.SaveChangesAsync();
        return (new(user.Id), subject);
    }
    [Fact]
    public async Task ImportPersistsDraftsAndReceiptAtomicallyAndRetryDoesNotDuplicate()
    {
        var (actor, subject) = await SeedAsync();
        await using var db = Scoped(actor);
        var service = new QuestionImportService(new QuestionFileReader(), new QuestionBankStore(db), actor);
        var bytes = Encoding.UTF8.GetBytes($"SubjectCode,Topic,Content,ExpectedAnswer,BloomLevel,Difficulty\n{subject.Code},,Explain DI,Expected,Understand,Medium");
        Assert.Empty((await service.PreviewAsync(bytes, "test.csv", default)).Errors);
        Assert.Equal(1, await service.ImportAsync(bytes, "test.csv", default));
        Assert.Equal(1, await service.ImportAsync(bytes, "test.csv", default));
        var question = Assert.Single(await db.Questions.ToArrayAsync());
        Assert.Equal(QuestionStatus.Draft, question.Status); Assert.Equal(QuestionSourceType.Imported, question.SourceType);
        Assert.Equal(actor.Id, question.CreatedBy);
        var invalid = Encoding.UTF8.GetBytes($"SubjectCode,Topic,Content,ExpectedAnswer,BloomLevel,Difficulty\n{subject.Code},,Valid,,Understand,Medium\nBAD,,Invalid,,BAD,Medium");
        await Assert.ThrowsAsync<ResourceConflictException>(() => service.ImportAsync(invalid, "bad.csv", default));
        Assert.Equal(1, await db.Questions.CountAsync());
    }
    [Fact]
    public async Task LecturerCannotReadAnotherOwnersQuestionsOrUnassignedSubjects()
    {
        var (owner, subject) = await SeedAsync();
        var (other, _) = await SeedAsync();
        Guid questionId;
        await using (var db = Scoped(owner))
        {
            var question = new Question(subject.Id, "Owned question", BloomLevel.Understand, QuestionDifficulty.Medium);
            db.Add(question); await db.SaveChangesAsync(); questionId = question.Id;
        }
        await using (var db = Scoped(other))
        {
            Assert.Null(await db.Questions.SingleOrDefaultAsync(x => x.Id == questionId));
            Assert.Null(await db.Subjects.SingleOrDefaultAsync(x => x.Id == subject.Id));
        }
        await using var admin = Scoped(new Actor(Guid.NewGuid(), true));
        Assert.NotNull(await admin.Questions.SingleOrDefaultAsync(x => x.Id == questionId));
    }
    private sealed class Extractor : IDocumentTextExtractor
    { public IReadOnlyList<ExtractedPage> Extract(byte[] content, string name) => [new(4, "Dependency injection supplies dependencies to a class.")]; }
    private sealed class Generator(bool invalidQuote = false) : IQuestionGenerator
    {
        public Task<IReadOnlyList<GeneratedDraft>> GenerateAsync(string instruction, BloomLevel bloom, QuestionDifficulty difficulty,
            int count, IReadOnlyList<DocumentChunk> sources, CancellationToken ct) => Task.FromResult<IReadOnlyList<GeneratedDraft>>(
                [new("What is dependency injection?", "It supplies dependencies.", sources[0].Id, invalidQuote ? "invented" : "supplies dependencies")]);
    }
    [Fact]
    public async Task GenerationSavesDraftWithExactCitationAndInvalidSourceSavesNothing()
    {
        var (actor, subject) = await SeedAsync();
        await using var db = Scoped(actor);
        var store = new QuestionBankStore(db);
        var service = new DocumentService(store, new Extractor(), new Generator(), actor);
        var doc = await service.UploadAsync(subject.Id, [1, 2, 3], "lecture.pdf", default);
        Assert.Equal(doc.Id, (await service.UploadAsync(subject.Id, [1, 2, 3], "lecture.pdf", default)).Id);
        var ids = await service.GenerateAsync(doc.Id, null, "dependency injection", BloomLevel.Understand, QuestionDifficulty.Medium, 1, default);
        Assert.Equal(QuestionStatus.Draft, (await db.Questions.SingleAsync(x => x.Id == ids[0])).Status);
        var citation = Assert.Single(await store.CitationsAsync(ids[0], default));
        Assert.Equal(4, citation.PageNumber); Assert.Equal("supplies dependencies", citation.Quote);
        var invalid = new DocumentService(store, new Extractor(), new Generator(true), actor);
        await Assert.ThrowsAsync<DomainValidationException>(() => invalid.GenerateAsync(doc.Id, null, "dependency injection", BloomLevel.Understand, QuestionDifficulty.Medium, 1, default));
        Assert.Single(await db.Questions.ToArrayAsync());
    }
}
