using AIpoweredVivaExamSystem.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AIpoweredVivaExamSystem.DataAccess.Configurations;

public sealed class StudentAnswerConfiguration : IEntityTypeConfiguration<StudentAnswer>
{
    public void Configure(EntityTypeBuilder<StudentAnswer> b)
    {
        b.ToTable("StudentAnswers", t =>
        {
            t.HasCheckConstraint("CK_StudentAnswers_FollowUpLevel", "[FollowUpLevel] >= 0");
            t.HasCheckConstraint("CK_StudentAnswers_AnsweredAt", "[AnsweredAt] IS NULL OR [AnsweredAt] >= [AskedAt]");
        });
        b.Property(x => x.AnswerType).HasConversion<string>().HasMaxLength(30).IsRequired();
        b.Property(x => x.PromptText).HasMaxLength(4000).IsRequired();
        b.Property(x => x.Transcript).HasMaxLength(8000);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.SessionQuestionId, x.FollowUpLevel }).IsUnique();
        b.HasOne<SessionQuestion>().WithMany().HasForeignKey(x => x.SessionQuestionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<StudentAnswer>().WithMany().HasForeignKey(x => x.ParentAnswerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AnswerAnalysisConfiguration : IEntityTypeConfiguration<AnswerAnalysis>
{
    public void Configure(EntityTypeBuilder<AnswerAnalysis> b)
    {
        b.ToTable("AnswerAnalyses");
        b.Property(x => x.MissingConcepts).HasMaxLength(2000);
        b.Property(x => x.AnalysisSummary).HasMaxLength(4000);
        b.Property(x => x.FollowUpPrompt).HasMaxLength(4000);
        b.Property(x => x.ModelName).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.StudentAnswerId).IsUnique();
        b.HasOne<StudentAnswer>().WithMany().HasForeignKey(x => x.StudentAnswerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AnswerAudioConfiguration : IEntityTypeConfiguration<AnswerAudio>
{
    public void Configure(EntityTypeBuilder<AnswerAudio> b)
    {
        b.ToTable("AnswerAudios", t =>
        {
            t.HasCheckConstraint("CK_AnswerAudios_FileSize", "[FileSizeBytes] > 0 AND [FileSizeBytes] <= 20971520");
            t.HasCheckConstraint("CK_AnswerAudios_Duration", "[DurationSeconds] IS NULL OR [DurationSeconds] >= 0");
        });
        b.Property(x => x.FilePath).HasMaxLength(1000).IsRequired();
        b.Property(x => x.MimeType).HasMaxLength(100).IsRequired();
        b.Property(x => x.ContentHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.StudentAnswerId).IsUnique();
        b.HasOne<StudentAnswer>().WithMany().HasForeignKey(x => x.StudentAnswerId).OnDelete(DeleteBehavior.Restrict);
    }
}

