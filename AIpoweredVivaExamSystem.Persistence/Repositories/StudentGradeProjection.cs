using System.Text.Json;
using AIpoweredVivaExamSystem.Application.Grading;
using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.EntityFrameworkCore;
namespace AIpoweredVivaExamSystem.Persistence.Repositories;

internal static class StudentGradeProjection
{
    // Called only after the own-session guard. The return type deliberately has no rubric/AI/evidence fields.
    public static async Task<StudentFinalResult?> LoadAsync(ApplicationDbContext db,Guid sessionId,CancellationToken ct)
    {
        var grade=await db.SessionGradings.IgnoreQueryFilters().AsNoTracking().Where(g=>g.VivaSessionId==sessionId && g.IsFinalised)
            .Select(g=>new {g.FinalisedAt,g.OverallComment}).SingleOrDefaultAsync(ct);
        if(grade is null) return null;
        var questions=await (from q in db.SessionQuestions.IgnoreQueryFilters().AsNoTracking()
            join g in db.QuestionGradings.IgnoreQueryFilters().AsNoTracking() on q.Id equals g.SessionQuestionId
            where q.VivaSessionId==sessionId orderby q.Position
            select new {q.Position,q.ContentSnapshot,q.RubricSnapshot,g.LecturerScoresJson,g.LecturerComment}).ToArrayAsync(ct);
        var results=questions.Select(q=>new StudentQuestionResult(q.Position,q.ContentSnapshot,
            JsonSerializer.Deserialize<CriterionGradeInput[]>(q.LecturerScoresJson!)!.Sum(s=>s.Score??0),
            GradingRules.ReadRubric(q.RubricSnapshot).Criteria.Sum(c=>c.MaxScore),q.LecturerComment)).ToArray();
        return new(results.Sum(q=>q.Score),results.Sum(q=>q.MaxScore),grade.FinalisedAt!.Value,grade.OverallComment,results);
    }
}
