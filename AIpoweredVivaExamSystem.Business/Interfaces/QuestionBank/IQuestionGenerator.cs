using AIpoweredVivaExamSystem.DataAccess.Entities;
using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.Business.QuestionBank;

public interface IQuestionGenerator
{
    Task<IReadOnlyList<GeneratedDraft>> GenerateAsync(string instruction, BloomLevel bloom,
        QuestionDifficulty difficulty, int count, IReadOnlyList<DocumentChunk> sources, CancellationToken ct);
}
