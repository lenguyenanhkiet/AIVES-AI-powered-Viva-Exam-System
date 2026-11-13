using System.Text.Json;
using AIpoweredVivaExamSystem.Business.VivaSessions;
using AIpoweredVivaExamSystem.DataAccess.Contracts.VivaSessions;
using AIpoweredVivaExamSystem.DataAccess.Models.VivaSessions;
using Microsoft.Extensions.Configuration;

namespace AIpoweredVivaExamSystem.Business.Integrations.VivaSessions;

public sealed class ElevenLabsScribeTokenProvider(HttpClient http, IConfiguration configuration) : IRealtimeScribeTokenProvider
{
    public async Task<ScribeToken?> CreateAsync(string language, CancellationToken ct)
    {
        var key = configuration["ElevenLabs:ApiKey"];
        if (string.IsNullOrWhiteSpace(key)) return null;
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.elevenlabs.io/v1/single-use-token/realtime_scribe")
        {
            Content = new ByteArrayContent([])
        };
        request.Headers.Add("xi-api-key", key);
        try
        {
            using var response = await AiRequest.SendAsync(http, request, ct);
            if (!response.IsSuccessStatusCode) return null;
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var token = json.RootElement.GetProperty("token").GetString();
            if (string.IsNullOrWhiteSpace(token)) return null;
            var model = configuration["ElevenLabs:RealtimeSttModel"];
            return new ScribeToken(token, string.IsNullOrWhiteSpace(model) ? "scribe_v3_live" : model.Trim(),
                ElevenLabsSpeechTranscriber.NormalizeLanguage(language));
        }
        catch (Exception ex) when (!ct.IsCancellationRequested &&
            ex is HttpRequestException or OperationCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }
}
