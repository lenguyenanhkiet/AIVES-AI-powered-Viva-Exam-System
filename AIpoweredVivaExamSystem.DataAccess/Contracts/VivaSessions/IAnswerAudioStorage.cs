using AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;
using AIpoweredVivaExamSystem.DataAccess.Models.Exams;
using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Contracts.VivaSessions;

public interface IAnswerAudioStorage
{
    Task<string> SaveAudioAsync(Guid sessionId, Guid turnId, byte[] content, string mimeType, CancellationToken ct);
    Task<Stream?> OpenReadAsync(string storedPath, CancellationToken ct) => Task.FromResult<Stream?>(null);
}
