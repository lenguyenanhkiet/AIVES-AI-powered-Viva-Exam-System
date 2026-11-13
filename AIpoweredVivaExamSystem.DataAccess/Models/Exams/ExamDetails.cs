using AIpoweredVivaExamSystem.DataAccess.Entities;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Exams;

public sealed record ExamDetails(ExamSummary Summary, int DurationMinutes, int MainQuestions, int MaxFollowUps, byte[] RowVersion);
