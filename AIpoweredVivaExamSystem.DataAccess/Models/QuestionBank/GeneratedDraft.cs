using AIpoweredVivaExamSystem.DataAccess.Entities;
using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.QuestionBank;

public sealed record GeneratedDraft(string Content, string ExpectedAnswer, Guid ChunkId, string Quote);
