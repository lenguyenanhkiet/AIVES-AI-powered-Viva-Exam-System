using AIpoweredVivaExamSystem.DataAccess.Entities;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Exams;

public sealed record ExamFoundationCounts(int PoolQuestions, int Students, int ScheduledStudents, int Sessions);
// This is the only session-question shape intended for student clients.
