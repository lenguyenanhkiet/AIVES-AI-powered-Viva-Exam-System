using AIpoweredVivaExamSystem.DataAccess.Entities;
using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Contracts.QuestionBank;

public interface IQuestionFileReader
{
    IReadOnlyList<ImportRow> Read(byte[] content, string fileName);
}
