using AIpoweredVivaExamSystem.DataAccess.Entities;
using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.QuestionBank;

public sealed record ImportRow(int RowNumber, string SubjectCode, string Topic, string Content,
    string ExpectedAnswer, string BloomLevel, string Difficulty);
