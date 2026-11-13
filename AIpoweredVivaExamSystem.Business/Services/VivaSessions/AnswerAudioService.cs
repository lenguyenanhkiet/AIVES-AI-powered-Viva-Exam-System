namespace AIpoweredVivaExamSystem.Business.VivaSessions;

public sealed class AnswerAudioService(IAnswerAudioStorage storage)
{
    // GradingService.AudioAsync checks ownership before giving Presentation the stored path.
    public Task<Stream?> OpenReadAsync(string storedPath, CancellationToken ct) => storage.OpenReadAsync(storedPath, ct);
}
