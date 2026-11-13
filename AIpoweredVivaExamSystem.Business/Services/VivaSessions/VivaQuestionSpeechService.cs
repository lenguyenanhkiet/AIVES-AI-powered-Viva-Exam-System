using AIpoweredVivaExamSystem.DataAccess.Models.Common;
using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.Business.VivaSessions;

public sealed class VivaQuestionSpeechService(IVivaInterviewService interview, IExaminerSpeechSynthesizer speech)
{
    public async Task<byte[]?> SpeakAsync(Guid sessionId, Guid turnId, CancellationToken ct)
    {
        // GetState enforces the student role and enrollment ownership before accessing cached audio.
        var state = await interview.GetStateAsync(sessionId, ct);
        var turn = state.Turns.SingleOrDefault(t => t.TurnId == turnId)
            ?? throw new ResourceNotFoundException("Turn not found.");
        var audio = speech.SynthesizeAsync(SpokenText(turn), ct);
        if (state.ActiveTurn?.TurnId == turnId)
        {
            var next = state.Questions.FirstOrDefault(q => q.Position > turn.MainPosition);
            if (next is not null) _ = WarmAsync(next.Content);
        }
        return await audio;
    }

    // The examiner announces a follow-up aloud so the student knows it is not a new main question.
    public static string SpokenText(VivaTurnItem turn) => turn.AnswerType == AnswerType.FollowUp
        ? $"Câu hỏi xoáy cho câu {turn.MainPosition}. {turn.PromptText}"
        : turn.PromptText;

    private async Task WarmAsync(string text)
    {
        // Only the singleton speech provider is used after this request ends. It bounds
        // synthesis to 12s and shares pending/cached work with the eventual Speak call.
        try { await speech.SynthesizeAsync(text, CancellationToken.None); }
        catch { /* Prefetch is optional; normal playback can retry or use browser speech. */ }
    }
}
