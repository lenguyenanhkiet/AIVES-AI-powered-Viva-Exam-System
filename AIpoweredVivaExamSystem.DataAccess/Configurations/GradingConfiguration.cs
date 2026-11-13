using AIpoweredVivaExamSystem.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace AIpoweredVivaExamSystem.DataAccess.Configurations;
public sealed class SessionGradingConfiguration : IEntityTypeConfiguration<SessionGrading>
{
    public void Configure(EntityTypeBuilder<SessionGrading> b)
    {
        b.ToTable("SessionGradings",t=>t.HasCheckConstraint("CK_SessionGradings_Finalised","[IsFinalised] = 0 OR ([FinalisedBy] IS NOT NULL AND [FinalisedAt] IS NOT NULL)"));
        b.HasIndex(x=>x.VivaSessionId).IsUnique(); b.Property(x=>x.OverallComment).HasMaxLength(4000);
        b.Property(x=>x.RowVersion).IsRowVersion();
        b.HasOne<VivaSession>().WithMany().HasForeignKey(x=>x.VivaSessionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x=>x.FinalisedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class QuestionGradingConfiguration : IEntityTypeConfiguration<QuestionGrading>
{
    public void Configure(EntityTypeBuilder<QuestionGrading> b)
    {
        b.ToTable("QuestionGradings"); b.HasIndex(x=>x.SessionQuestionId).IsUnique();
        b.Property(x=>x.AiModel).HasMaxLength(100); b.Property(x=>x.SuggestionError).HasMaxLength(1000);
        b.Property(x=>x.LecturerComment).HasMaxLength(2000);
        b.HasOne<SessionQuestion>().WithMany().HasForeignKey(x=>x.SessionQuestionId).OnDelete(DeleteBehavior.Restrict);
    }
}
