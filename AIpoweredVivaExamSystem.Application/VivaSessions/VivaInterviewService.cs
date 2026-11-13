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
        var receivedAt = DateTimeOffset.UtcNow;
        RequireStudent();
        DomainRules.RequiredId(request.SessionId, nameof(request.SessionId));
        DomainRules.RequiredId(request.TurnId, nameof(request.TurnId));

        var enrollmentId = await sessionStore.FindEnrollmentAsync(request.SessionId, ct);
        await guard.OwnExamStudentAsync(enrollmentId, ct);

        await using var submission = await interviewStore.AcquireSubmissionAsync(request.SessionId, ct);
        var state = await interviewStore.GetStateAsync(request.SessionId, ct);
        var turn = state.Turns.SingleOrDefault(t => t.TurnId == request.TurnId)
            ?? throw new ResourceNotFoundException("Turn not found.");
        if (turn.AnsweredAt.HasValue) return state;
        if (state.CompletedAt.HasValue)
            throw new DomainValidationException("Phiên thi đã kết thúc, không thể nộp thêm câu trả lời.");
        if (state.ActiveTurn?.TurnId != turn.TurnId)
            throw new ResourceConflictException("The active turn has changed. Reload the interview.");
        var timedOut = receivedAt >= state.SlotEndAt ||
            VivaTurnDeadline.HasExpired(state.SlotEndAt, receivedAt, request.TimedOut) ||
            VivaTurnDeadline.HasExpired(turn.AskedAt.AddSeconds(state.TurnLimitSeconds), receivedAt, request.TimedOut);

        var tooLate = receivedAt > state.SlotEndAt.AddSeconds(VivaTurnDeadline.GraceSeconds) ||
            receivedAt > turn.AskedAt.AddSeconds(state.TurnLimitSeconds + VivaTurnDeadline.GraceSeconds);
        if (tooLate)
            request = request with { Transcript = VivaTurnDeadline.NoAnswerMarker, AudioBytes = null };

        var transcript = request.Transcript?.Trim();
        if (string.IsNullOrWhiteSpace(transcript) && request.AudioBytes is { Length: > 0 })
        {
            var hint = await interviewStore.GetSubjectHintAsync(request.SessionId, ct);
            var ext = GuessExtension(request.AudioMimeType);
            try
            {
                transcript = await transcriber.TranscribeAsync(request.AudioBytes, $"answer{ext}", request.Language, hint, ct);
            }
            catch (Exception ex) when (timedOut && !ct.IsCancellationRequested &&
                ex is ResourceConflictException or HttpRequestException or OperationCanceledException)
            {
                // Preserve timeout submissions even when the speech provider cannot transcribe them.
            }
        }

        if (timedOut && string.IsNullOrWhiteSpace(transcript))
            transcript = VivaTurnDeadline.NoAnswerMarker;

        if (string.IsNullOrWhiteSpace(transcript))
            throw new DomainValidationException("Vui lòng trả lời bằng giọng nói hoặc nhập nội dung câu trả lời trước khi nộp.");

        var normalizedRequest = request with { Transcript = transcript.Trim() };
        return await interviewStore.SubmitAnswerAsync(normalizedRequest, receivedAt, ct);
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

