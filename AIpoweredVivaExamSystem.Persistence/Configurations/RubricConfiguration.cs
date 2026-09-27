using AIpoweredVivaExamSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AIpoweredVivaExamSystem.Persistence.Configurations;

public class RubricConfiguration : IEntityTypeConfiguration<Rubric>
{
    public void Configure(EntityTypeBuilder<Rubric> builder)
    {
        builder.ToTable("Rubrics", table =>
            table.HasCheckConstraint("CK_Rubrics_TotalScore", "[TotalScore] > 0 AND [TotalScore] <= 999.99"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(255);
        builder.Property(x => x.TotalScore).HasPrecision(5, 2).IsRequired();
        builder.HasIndex(x => x.QuestionId).IsUnique();
        builder.HasOne<Question>().WithOne().HasForeignKey<Rubric>(x => x.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Criteria).WithOne().HasForeignKey(x => x.RubricId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Criteria).HasField("_criteria").UsePropertyAccessMode(PropertyAccessMode.Field);
        // Protect the aggregate against overlapping updates, including criterion-only edits.
        builder.Property<byte[]>("RowVersion").IsRowVersion();
    }
}
