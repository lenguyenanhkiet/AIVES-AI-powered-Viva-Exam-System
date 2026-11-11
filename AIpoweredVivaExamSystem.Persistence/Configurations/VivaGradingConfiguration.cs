using AIpoweredVivaExamSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AIpoweredVivaExamSystem.Persistence.Configurations;

public sealed class AiGradingConfiguration : IEntityTypeConfiguration<AiGrading>
{
    public void Configure(EntityTypeBuilder<AiGrading> b)
    {
        b.ToTable("AiGradings", t =>
        {
            t.HasCheckConstraint("CK_AiGradings_MaxScore", "[MaxScore] > 0 AND [MaxScore] <= 999.99");
            t.HasCheckConstraint("CK_AiGradings_AiScore", "[AiScore] >= 0 AND [AiScore] <= [MaxScore]");
            t.HasCheckConstraint("CK_AiGradings_ResponseSeconds", "[TotalResponseSeconds] >= 0");
            t.HasCheckConstraint("CK_AiGradings_WordsPerMinute", "[WordsPerMinute] >= 0");
        });
        b.Property(x => x.AiScore).HasPrecision(5, 2).IsRequired();
        b.Property(x => x.MaxScore).HasPrecision(5, 2).IsRequired();
        b.Property(x => x.AiComment).HasMaxLength(4000);
        b.Property(x => x.Strengths).HasMaxLength(4000);
        b.Property(x => x.Weaknesses).HasMaxLength(4000);
        b.Property(x => x.MissingPoints).HasMaxLength(4000);
        b.Property(x => x.FluencyNote).HasMaxLength(1000);
        b.Property(x => x.ModelName).HasMaxLength(100).IsRequired();
        b.Property(x => x.PromptVersion).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.SessionQuestionId).IsUnique();
        b.HasOne<SessionQuestion>().WithMany().HasForeignKey(x => x.SessionQuestionId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Criteria).WithOne().HasForeignKey(x => x.AiGradingId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AiGradingCriterionConfiguration : IEntityTypeConfiguration<AiGradingCriterion>
{
    public void Configure(EntityTypeBuilder<AiGradingCriterion> b)
    {
        b.ToTable("AiGradingCriteria", t =>
        {
            t.HasCheckConstraint("CK_AiGradingCriteria_MaxScore", "[MaxScore] > 0 AND [MaxScore] <= 999.99");
            t.HasCheckConstraint("CK_AiGradingCriteria_Score", "[Score] >= 0 AND [Score] <= [MaxScore]");
            t.HasCheckConstraint("CK_AiGradingCriteria_DisplayOrder", "[DisplayOrder] >= 1");
        });
        b.Property(x => x.CriterionName).HasMaxLength(255).IsRequired();
        b.Property(x => x.MaxScore).HasPrecision(5, 2).IsRequired();
        b.Property(x => x.Score).HasPrecision(5, 2).IsRequired();
        b.Property(x => x.Evidence).HasMaxLength(2000);
        b.Property(x => x.Comment).HasMaxLength(2000);
        b.HasIndex(x => new { x.AiGradingId, x.DisplayOrder }).IsUnique();
    }
}

public sealed class TeacherReviewConfiguration : IEntityTypeConfiguration<TeacherReview>
{
    public void Configure(EntityTypeBuilder<TeacherReview> b)
    {
        b.ToTable("TeacherReviews", t =>
        {
            t.HasCheckConstraint("CK_TeacherReviews_MaxScore", "[MaxScore] > 0 AND [MaxScore] <= 999.99");
            t.HasCheckConstraint("CK_TeacherReviews_FinalScore", "[FinalScore] >= 0 AND [FinalScore] <= [MaxScore]");
            t.HasCheckConstraint("CK_TeacherReviews_TeacherScore", "[TeacherScore] >= 0 AND [TeacherScore] <= [MaxScore]");
        });
        b.Property(x => x.AiScoreSnapshot).HasPrecision(5, 2);
        b.Property(x => x.TeacherScore).HasPrecision(5, 2).IsRequired();
        b.Property(x => x.FinalScore).HasPrecision(5, 2).IsRequired();
        b.Property(x => x.MaxScore).HasPrecision(5, 2).IsRequired();
        b.Property(x => x.Comment).HasMaxLength(4000);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.SessionQuestionId).IsUnique();
        b.HasOne<SessionQuestion>().WithMany().HasForeignKey(x => x.SessionQuestionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.LecturerId).OnDelete(DeleteBehavior.Restrict);
    }
}
