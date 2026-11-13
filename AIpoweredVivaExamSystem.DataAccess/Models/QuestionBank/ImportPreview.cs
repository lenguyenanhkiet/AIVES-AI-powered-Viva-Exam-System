using AIpoweredVivaExamSystem.DataAccess.Entities;
using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.QuestionBank;

public sealed record ImportPreview(int ValidCount, IReadOnlyList<ImportRow> Rows, IReadOnlyList<ImportError> Errors);
