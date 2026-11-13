using AIpoweredVivaExamSystem.DataAccess.Entities;
using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Contracts.QuestionBank;

public interface IDocumentTextExtractor
{
    IReadOnlyList<ExtractedPage> Extract(byte[] content, string fileName);
}
