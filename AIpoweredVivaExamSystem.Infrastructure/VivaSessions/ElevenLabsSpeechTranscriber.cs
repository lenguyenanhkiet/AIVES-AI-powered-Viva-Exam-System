using System.Net.Http.Headers;
using System.Text.Json;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.VivaSessions;
using Microsoft.Extensions.Configuration;

namespace AIpoweredVivaExamSystem.Infrastructure.VivaSessions;

public sealed class ElevenLabsSpeechTranscriber(HttpClient http, IConfiguration configuration) : ISpeechTranscriber
{
    public async Task<string> TranscribeAsync(byte[] audioBytes, string fileName, string language, string? promptHint, CancellationToken ct)
    {
        var key = configuration["ElevenLabs:ApiKey"];
        if (string.IsNullOrWhiteSpace(key))
            throw new ResourceConflictException("Speech transcription is not configured. Use browser speech recognition or type your answer.");

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(audioBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", Path.GetFileName(string.IsNullOrWhiteSpace(fileName) ? "answer.webm" : fileName));
        var model = configuration["ElevenLabs:SttModel"];
        form.Add(new StringContent(string.IsNullOrWhiteSpace(model) ? "scribe_v3" : model.Trim()), "model_id");
        form.Add(new StringContent(NormalizeLanguage(language)), "language_code");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.elevenlabs.io/v1/speech-to-text") { Content = form };
        request.Headers.Add("xi-api-key", key);
        try
        {
            using var response = await AiRequest.SendAsync(http, request, ct);
            if (!response.IsSuccessStatusCode)
                throw new ResourceConflictException("Speech transcription is unavailable. Use browser speech recognition or type your answer.");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var text = json.RootElement.GetProperty("text").GetString()?.Trim();
            return string.IsNullOrWhiteSpace(text)
                ? throw new ResourceConflictException("No speech was recognized. Please record again or type your answer.")
                : text;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested &&
            ex is HttpRequestException or OperationCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new ResourceConflictException("Speech transcription is unavailable. Use browser speech recognition or type your answer.");
        }
    }

    internal static string NormalizeLanguage(string? language) =>
        language?.Split('-', '_')[0].Trim().Equals("en", StringComparison.OrdinalIgnoreCase) == true ? "en" : "vi";
}
