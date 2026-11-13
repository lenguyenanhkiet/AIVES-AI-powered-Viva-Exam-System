using AIpoweredVivaExamSystem.DataAccess.Models.Common;

namespace AIpoweredVivaExamSystem.Business.VivaSessions;

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
