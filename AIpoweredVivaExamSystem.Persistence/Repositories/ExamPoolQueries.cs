using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace AIpoweredVivaExamSystem.Persistence.Repositories;

/// <summary>One eligibility rule shared by the pool picker, publish counts, and session selection.</summary>
internal static class ExamPoolQueries
{
    public static IQueryable<Question> EligibleQuestionBank(ApplicationDbContext db, Exam exam,
        IQueryable<Question>? questionSource = null)
    {
        var questions = questionSource ?? db.Questions.IgnoreQueryFilters();
        return questions.Where(q => q.SubjectId == exam.SubjectId && q.CreatedBy == exam.LecturerId &&
                q.DeletedAt == null && q.Status == QuestionStatus.Approved)
            .Where(_ => db.LecturerSubjects.Any(a => a.LecturerId == exam.LecturerId && a.SubjectId == exam.SubjectId))
            .Where(q => db.Rubrics.IgnoreQueryFilters().Any(r => r.QuestionId == q.Id && r.DeletedAt == null));
    }

    public static IQueryable<Question> EligiblePooledQuestions(ApplicationDbContext db, Exam exam,
        IQueryable<Question>? questionSource = null)
    {
        var pool = db.ExamQuestionPools.IgnoreQueryFilters().Where(p => p.ExamId == exam.Id);
        return EligibleQuestionBank(db, exam, questionSource).Where(q => pool.Any(p => p.QuestionId == q.Id));
    }
}
