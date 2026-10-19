using AIpoweredVivaExamSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AIpoweredVivaExamSystem.Persistence.Configurations;

public sealed class LecturerSubjectConfiguration : IEntityTypeConfiguration<LecturerSubject>
{
    public void Configure(EntityTypeBuilder<LecturerSubject> b)
    {
        b.HasIndex(x => new { x.LecturerId, x.SubjectId }).IsUnique();
        b.HasOne<User>().WithMany().HasForeignKey(x => x.LecturerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class CourseDocumentConfiguration : IEntityTypeConfiguration<CourseDocument>
{
    public void Configure(EntityTypeBuilder<CourseDocument> b)
    {
        b.Property(x => x.FileName).HasMaxLength(255);
        b.Property(x => x.ContentHash).HasMaxLength(64);
        b.HasIndex(x => new { x.SubjectId, x.CreatedBy, x.ContentHash }).IsUnique();
        b.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> b)
    {
        b.Property(x => x.Text).HasMaxLength(2400);
        b.HasOne<CourseDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class QuestionCitationConfiguration : IEntityTypeConfiguration<QuestionCitation>
{
    public void Configure(EntityTypeBuilder<QuestionCitation> b)
    {
        b.Property(x => x.Quote).HasMaxLength(2400);
        b.HasOne<Question>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DocumentChunk>().WithMany().HasForeignKey(x => x.DocumentChunkId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class QuestionImportBatchConfiguration : IEntityTypeConfiguration<QuestionImportBatch>
{
    public void Configure(EntityTypeBuilder<QuestionImportBatch> b)
    {
        b.Property(x => x.ContentHash).HasMaxLength(64);
        b.HasIndex(x => new { x.UserId, x.ContentHash }).IsUnique();
    }
}
