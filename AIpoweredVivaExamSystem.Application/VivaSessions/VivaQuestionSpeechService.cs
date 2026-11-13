using AIpoweredVivaExamSystem.Application.Common;

namespace AIpoweredVivaExamSystem.Application.VivaSessions;

public interface IExaminerSpeechSynthesizer
{
    Task<byte[]?> SynthesizeAsync(string text, CancellationToken ct);
}

public sealed class VivaQuestionSpeechService(IVivaInterviewService interview, IExaminerSpeechSynthesizer speech)
{
    public async Task<byte[]?> SpeakAsync(Guid sessionId, Guid turnId, CancellationToken ct)
    {
        // GetState enforces the student role and enrollment ownership before accessing cached audio.
        var state = await interview.GetStateAsync(sessionId, ct);
        var turn = state.Turns.SingleOrDefault(t => t.TurnId == turnId)
            ?? throw new ResourceNotFoundException("Turn not found.");
        return await speech.SynthesizeAsync(turn.PromptText, ct);
    }
}
