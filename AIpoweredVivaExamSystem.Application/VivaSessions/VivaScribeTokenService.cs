using AIpoweredVivaExamSystem.Application.Common;

namespace AIpoweredVivaExamSystem.Application.VivaSessions;

public sealed record ScribeToken(string Token, string ModelId, string LanguageCode);

public interface IRealtimeScribeTokenProvider
{
    Task<ScribeToken?> CreateAsync(string language, CancellationToken ct);
}

public sealed class VivaScribeTokenService(IVivaInterviewService interview, IRealtimeScribeTokenProvider tokens)
{
    public async Task<ScribeToken?> CreateAsync(Guid sessionId, string language, CancellationToken ct)
    {
        var state = await interview.GetStateAsync(sessionId, ct);
        if (state.CompletedAt.HasValue)
            throw new ResourceConflictException("The interview is already completed.");
        return await tokens.CreateAsync(language, ct);
    }
}
