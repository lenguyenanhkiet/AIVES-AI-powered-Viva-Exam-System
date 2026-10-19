using AIpoweredVivaExamSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AIpoweredVivaExamSystem.Persistence.Configurations;

public sealed class ExamConfiguration : IEntityTypeConfiguration<Exam>
{
    public void Configure(EntityTypeBuilder<Exam> b)
    {
        b.ToTable("Exams", t => {
            t.HasCheckConstraint("CK_Exams_Window", "[StartAt] < [EndAt]");
            t.HasCheckConstraint("CK_Exams_Counts", "[DurationMinutes] > 0 AND [NumberOfMainQuestions] > 0 AND [MaxFollowUpPerQuestion] >= 0");
        });
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.LecturerId, x.StartAt });
        b.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.LecturerId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class ExamStudentConfiguration : IEntityTypeConfiguration<ExamStudent>
{
    public void Configure(EntityTypeBuilder<ExamStudent> b)
    {
        b.ToTable("ExamStudents"); b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.ExamId, x.StudentId }).IsUnique();
        b.HasIndex(x => new { x.StudentId, x.ScheduledAt });
        b.HasOne<Exam>().WithMany().HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class ExamQuestionPoolConfiguration : IEntityTypeConfiguration<ExamQuestionPool>
{
    public void Configure(EntityTypeBuilder<ExamQuestionPool> b)
    {
        b.ToTable("ExamQuestionPools"); b.HasIndex(x => new { x.ExamId, x.QuestionId }).IsUnique();
        b.HasOne<Exam>().WithMany().HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Question>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class VivaSessionConfiguration : IEntityTypeConfiguration<VivaSession>
{
    public void Configure(EntityTypeBuilder<VivaSession> b)
    {
        b.ToTable("VivaSessions", t => t.HasCheckConstraint("CK_VivaSessions_Completion", "[CompletedAt] IS NULL OR [CompletedAt] >= [StartedAt]"));
        b.Property(x => x.RowVersion).IsRowVersion(); b.HasIndex(x => x.ExamStudentId).IsUnique();
        b.HasOne<ExamStudent>().WithMany().HasForeignKey(x => x.ExamStudentId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class SessionQuestionConfiguration : IEntityTypeConfiguration<SessionQuestion>
{
    public void Configure(EntityTypeBuilder<SessionQuestion> b)
    {
        b.ToTable("SessionQuestions", t => {
            t.HasCheckConstraint("CK_SessionQuestions_Position", "[Position] > 0");
            t.HasCheckConstraint("CK_SessionQuestions_RubricJson", "ISJSON([RubricSnapshot]) = 1");
        });
        b.Property(x => x.ContentSnapshot).HasMaxLength(4000).IsRequired();
        b.Property(x => x.RubricSnapshot).IsRequired();
        b.HasIndex(x => new { x.VivaSessionId, x.Position }).IsUnique();
        b.HasIndex(x => new { x.VivaSessionId, x.QuestionId }).IsUnique();
        b.HasOne<VivaSession>().WithMany().HasForeignKey(x => x.VivaSessionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Question>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
    }
}
