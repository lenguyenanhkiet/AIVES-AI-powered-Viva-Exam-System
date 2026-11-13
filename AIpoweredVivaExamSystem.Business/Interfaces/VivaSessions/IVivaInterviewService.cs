using AIpoweredVivaExamSystem.Business.Exams;
using AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;
using AIpoweredVivaExamSystem.DataAccess.Models.Exams;
using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.Business.VivaSessions;

public interface IVivaInterviewService
{
    Task<VivaInterviewState> OpenOrResumeByEnrollmentAsync(Guid enrollmentId, CancellationToken ct);
    Task<VivaInterviewState> GetStateAsync(Guid sessionId, CancellationToken ct);
    Task<VivaInterviewState> SubmitAnswerAsync(SubmitAnswerRequest request, CancellationToken ct);
    Task<string> TranscribeAudioAsync(Guid sessionId, byte[] audioBytes, string fileName, string language, CancellationToken ct);
}
