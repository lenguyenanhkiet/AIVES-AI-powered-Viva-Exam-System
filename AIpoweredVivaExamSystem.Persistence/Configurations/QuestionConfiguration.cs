using AIpoweredVivaExamSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AIpoweredVivaExamSystem.Persistence.Configurations;

public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("Questions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Content).IsRequired();
        builder.Property(x => x.BloomLevel).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.Difficulty).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.SourceType).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
        // The composite FK also prevents assigning a topic from a different subject.
        builder.HasOne<Topic>().WithMany().HasForeignKey(x => new { x.TopicId, x.SubjectId })
            .HasPrincipalKey(x => new { x.Id, x.SubjectId }).OnDelete(DeleteBehavior.Restrict);
    }
}
