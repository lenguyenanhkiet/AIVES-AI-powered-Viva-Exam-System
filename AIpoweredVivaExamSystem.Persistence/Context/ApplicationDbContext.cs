using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIpoweredVivaExamSystem.Persistence.Context;

public class ApplicationDbContext : DbContext
{
    private readonly AIpoweredVivaExamSystem.Application.Authentication.ICurrentUser? currentUser;
    private bool Unrestricted => currentUser is null || currentUser.IsAdministrator;
    private Guid ActorId => currentUser?.Id ?? Guid.Empty;
    private bool LecturerScope => currentUser?.IsLecturer == true;
    public ApplicationDbContext(
       DbContextOptions<ApplicationDbContext> options,
       AIpoweredVivaExamSystem.Application.Authentication.ICurrentUser? currentUser = null)
       : base(options)
    {
        this.currentUser = currentUser;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamStudent> ExamStudents => Set<ExamStudent>();
    public DbSet<ExamQuestionPool> ExamQuestionPools => Set<ExamQuestionPool>();
    public DbSet<VivaSession> VivaSessions => Set<VivaSession>();
    public DbSet<SessionQuestion> SessionQuestions => Set<SessionQuestion>();
    public DbSet<LecturerSubject> LecturerSubjects => Set<LecturerSubject>();
    public DbSet<CourseDocument> CourseDocuments => Set<CourseDocument>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<QuestionCitation> QuestionCitations => Set<QuestionCitation>();
    public DbSet<QuestionImportBatch> QuestionImportBatches => Set<QuestionImportBatch>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Rubric> Rubrics => Set<Rubric>();
    public DbSet<RubricCriterion> RubricCriteria => Set<RubricCriterion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ApplicationDbContext).Assembly);
        modelBuilder.Entity<Subject>().HasQueryFilter(x => Unrestricted || LecturerSubjects.Any(a => a.SubjectId == x.Id && a.LecturerId == ActorId));
        modelBuilder.Entity<Topic>().HasQueryFilter(x => Unrestricted || LecturerSubjects.Any(a => a.SubjectId == x.SubjectId && a.LecturerId == ActorId));
        modelBuilder.Entity<Question>().HasQueryFilter(x => Unrestricted || (x.CreatedBy == ActorId && LecturerSubjects.Any(a => a.SubjectId == x.SubjectId && a.LecturerId == ActorId)));
        modelBuilder.Entity<Rubric>().HasQueryFilter(x => Unrestricted || Questions.Any(q => q.Id == x.QuestionId));
        modelBuilder.Entity<CourseDocument>().HasQueryFilter(x => Unrestricted || (x.CreatedBy == ActorId && LecturerSubjects.Any(a => a.SubjectId == x.SubjectId && a.LecturerId == ActorId)));
        modelBuilder.Entity<DocumentChunk>().HasQueryFilter(x => Unrestricted || CourseDocuments.Any(d => d.Id == x.DocumentId));
        modelBuilder.Entity<QuestionCitation>().HasQueryFilter(x => Unrestricted || Questions.Any(q => q.Id == x.QuestionId));
        modelBuilder.Entity<Exam>().HasQueryFilter(x => Unrestricted ||
            (LecturerScope && x.LecturerId == ActorId && LecturerSubjects.Any(a => a.LecturerId == ActorId && a.SubjectId == x.SubjectId)));
        modelBuilder.Entity<ExamStudent>().HasQueryFilter(x => Unrestricted || x.StudentId == ActorId || Exams.Any(e => e.Id == x.ExamId));
        modelBuilder.Entity<ExamQuestionPool>().HasQueryFilter(x => Unrestricted || Exams.Any(e => e.Id == x.ExamId));
        modelBuilder.Entity<VivaSession>().HasQueryFilter(x => Unrestricted || ExamStudents.Any(e => e.Id == x.ExamStudentId));
        // Internal entities are manager-only; student modules must explicitly project the safe DTO after the own-enrollment guard.
        modelBuilder.Entity<SessionQuestion>().HasQueryFilter(x => Unrestricted || VivaSessions.Any(s => s.Id == x.VivaSessionId &&
            ExamStudents.Any(r => r.Id == s.ExamStudentId && Exams.Any(e => e.Id == r.ExamId))));
    }

    public override Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        UpdateAuditableEntities();

        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateAuditableEntities()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    if (currentUser is not null) entry.Entity.CreatedBy = ActorId;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    if (currentUser is not null) entry.Entity.UpdatedBy = ActorId;
                    break;
            }
        }
    }
}
