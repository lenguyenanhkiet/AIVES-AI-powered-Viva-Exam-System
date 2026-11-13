using AIpoweredVivaExamSystem.Business.Authentication;
using AIpoweredVivaExamSystem.DataAccess.Contracts.Authentication;
using AIpoweredVivaExamSystem.DataAccess.Models.Authentication;
using AIpoweredVivaExamSystem.DataAccess.Models.Common;
using AIpoweredVivaExamSystem.Business.Exams;
using AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;
using AIpoweredVivaExamSystem.DataAccess.Models.Exams;
using AIpoweredVivaExamSystem.DataAccess.Common;

namespace AIpoweredVivaExamSystem.Business.VivaSessions;

public sealed class VivaInterviewService(
    IVivaSessionStore sessionStore,
    IVivaInterviewStore interviewStore,
    ISpeechTranscriber transcriber,
    ExamAccessGuard guard,
    ICurrentUser actor,
    IVivaAiExaminer examiner) : IVivaInterviewService
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

        if (request.AudioBytes is { Length: > 0 } recording)
            request = request with { AudioMimeType = AnswerRecording.Validate(recording, request.AudioMimeType) };

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

        var normalizedRequest = request with
        {
            Transcript = DomainRules.RequiredText(transcript, nameof(request.Transcript), 8000)
        };
        // Ask the external examiner in Business, outside the short database transaction.
        var decision = timedOut
            ? new ExaminerDecision(false, null, "Turn deadline expired; advanced without AI evaluation.", null, VivaTurnDeadline.TimeoutModelName)
            : new ExaminerDecision(false, null, "Không còn lượt hỏi xoáy; lưu câu trả lời để giảng viên chấm theo rubric.", null, "server-no-follow-up");
        if (!timedOut && turn.FollowUpLevel < state.MaxFollowUpPerQuestion && (state.SlotEndAt - receivedAt).TotalSeconds >= 15)
        {
            var context = await interviewStore.ReadEvaluationContextAsync(normalizedRequest, ct);
            decision = await examiner.EvaluateAndFollowUpAsync(context, ct);
        }
        return await interviewStore.SubmitAnswerAsync(normalizedRequest, receivedAt, decision, ct);
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

    public static int CalculateTurnLimitSeconds(int durationMinutes, int mainQuestions, int maxFollowUps) =>
        VivaTiming.CalculateTurnLimitSeconds(durationMinutes, mainQuestions, maxFollowUps);

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
