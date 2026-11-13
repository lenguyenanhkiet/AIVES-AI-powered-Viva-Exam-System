using AIpoweredVivaExamSystem.DataAccess.Models.Common;

namespace AIpoweredVivaExamSystem.Business.VivaSessions;

public interface IRealtimeScribeTokenProvider
{
    Task<ScribeToken?> CreateAsync(string language, CancellationToken ct);
}
