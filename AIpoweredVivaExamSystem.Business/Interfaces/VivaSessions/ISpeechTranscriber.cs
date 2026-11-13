using AIpoweredVivaExamSystem.Business.Exams;
using AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;
using AIpoweredVivaExamSystem.DataAccess.Models.Exams;
using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.Business.VivaSessions;

public interface ISpeechTranscriber
{
    Task<string> TranscribeAsync(byte[] audioBytes, string fileName, string language, string? promptHint, CancellationToken ct);
}
