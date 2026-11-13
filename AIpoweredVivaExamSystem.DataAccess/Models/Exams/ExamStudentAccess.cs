using AIpoweredVivaExamSystem.DataAccess.Entities;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Exams;

public sealed record ExamStudentAccess(Guid Id, Guid ExamId, Guid StudentId);
