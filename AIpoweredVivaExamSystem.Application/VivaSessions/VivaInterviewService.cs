using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Domain.Common;

namespace AIpoweredVivaExamSystem.Application.VivaSessions;

public sealed class VivaInterviewService(
    IVivaSessionStore sessionStore,
    IVivaInterviewStore interviewStore,
    ISpeechTranscriber transcriber,
    ExamAccessGuard guard,
    ICurrentUser actor) : IVivaInterviewService
{
    public async Task<VivaInterviewState> OpenOrResumeByEnrollmentAsync(Guid enrollmentId, CancellationToken ct)
    {
        RequireStudent();
        await guard.OwnExamStudentAsync(enrollmentId, ct);
        var session = await sessionStore.GetOrCreateAsync(enrollmentId, ct);
        return await interviewStore.GetStateAsync(session.Id, ct);
    }

    public async Task<VivaInterviewState> GetStateAsync(Guid sessionId, CancellationToken ct)
    {
        RequireStudent();
        var enrollmentId = await sessionStore.FindEnrollmentAsync(sessionId, ct);
        await guard.OwnExamStudentAsync(enrollmentId, ct);
        return await interviewStore.GetStateAsync(sessionId, ct);
    }

    public async Task<VivaInterviewState> SubmitAnswerAsync(SubmitAnswerRequest request, CancellationToken ct)
    {
        RequireStudent();
        DomainRules.RequiredId(request.SessionId, nameof(request.SessionId));
        DomainRules.RequiredId(request.TurnId, nameof(request.TurnId));

        var enrollmentId = await sessionStore.FindEnrollmentAsync(request.SessionId, ct);
        await guard.OwnExamStudentAsync(enrollmentId, ct);

        var transcript = request.Transcript?.Trim();
        if (string.IsNullOrWhiteSpace(transcript) && request.AudioBytes is { Length: > 0 })
        {
            var hint = await interviewStore.GetSubjectHintAsync(request.SessionId, ct);
            var ext = GuessExtension(request.AudioMimeType);
            transcript = await transcriber.TranscribeAsync(request.AudioBytes, $"answer{ext}", request.Language, hint, ct);
        }

        if (string.IsNullOrWhiteSpace(transcript))
            throw new DomainValidationException("Vui lòng trả lời bằng giọng nói hoặc nhập nội dung câu trả lời trước khi nộp.");

        var normalizedRequest = request with { Transcript = transcript.Trim() };
        return await interviewStore.SubmitAnswerAsync(normalizedRequest, ct);
    }

    public async Task<string> TranscribeAudioAsync(Guid sessionId, byte[] audioBytes, string fileName, string language, CancellationToken ct)
    {
        RequireStudent();
        if (audioBytes is null || audioBytes.Length is 0 or > 20 * 1024 * 1024)
            throw new ResourceConflictException("File ghi âm phải từ 1 byte đến 20 MB.");

        var enrollmentId = await sessionStore.FindEnrollmentAsync(sessionId, ct);
        await guard.OwnExamStudentAsync(enrollmentId, ct);
        var hint = await interviewStore.GetSubjectHintAsync(sessionId, ct);
        return await transcriber.TranscribeAsync(audioBytes, fileName, language, hint, ct);
    }

    public static int CalculateTurnLimitSeconds(int durationMinutes, int mainQuestions, int maxFollowUps)
    {
        var totalPossibleTurns = Math.Max(1, mainQuestions * (1 + Math.Max(0, maxFollowUps)));
        var rawSeconds = (Math.Max(1, durationMinutes) * 60) / totalPossibleTurns;
        return Math.Clamp(rawSeconds, 30, 300);
    }

    private static string GuessExtension(string? mimeType) =>
        mimeType?.ToLowerInvariant() switch
        {
            var m when m is not null && m.Contains("wav") => ".wav",
            var m when m is not null && m.Contains("mp4") => ".mp4",
            var m when m is not null && m.Contains("mpeg") || (m is not null && m.Contains("mp3")) => ".mp3",
            var m when m is not null && m.Contains("ogg") => ".ogg",
            _ => ".webm"
        };

    private void RequireStudent()
    {
        if (actor.Id == Guid.Empty || actor.IsAdministrator || actor.IsLecturer)
            throw new UnauthorizedAccessException("Student access is required.");
    }
}

