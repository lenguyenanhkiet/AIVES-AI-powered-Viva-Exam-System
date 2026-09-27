using AIpoweredVivaExamSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AIpoweredVivaExamSystem.Persistence.Configurations;

public class RubricCriterionConfiguration : IEntityTypeConfiguration<RubricCriterion>
{
    public void Configure(EntityTypeBuilder<RubricCriterion> builder)
    {
        builder.ToTable("RubricCriteria", table =>
        {
            table.HasCheckConstraint("CK_RubricCriteria_MaxScore", "[MaxScore] > 0 AND [MaxScore] <= 999.99");
            table.HasCheckConstraint("CK_RubricCriteria_DisplayOrder", "[DisplayOrder] >= 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(255);
        builder.Property(x => x.MaxScore).HasPrecision(5, 2).IsRequired();
        // Ordering uniqueness is validated by the aggregate, allowing order swaps in one save.
        builder.HasIndex(x => new { x.RubricId, x.DisplayOrder });
    }
}
