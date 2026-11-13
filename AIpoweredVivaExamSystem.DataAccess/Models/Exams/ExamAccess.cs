using AIpoweredVivaExamSystem.DataAccess.Entities;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Exams;

public sealed record ExamAccess(Guid Id, Guid SubjectId, Guid LecturerId);
