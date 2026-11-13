using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using AIpoweredVivaExamSystem.Application.VivaSessions;

namespace AIpoweredVivaExamSystem.Infrastructure.VivaSessions;

public sealed class ElevenLabsSpeechSynthesizer(
    IHttpClientFactory clients,
    IConfiguration configuration,
    ILogger<ElevenLabsSpeechSynthesizer> logger) : IExaminerSpeechSynthesizer, IDisposable
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 64 * 1024 * 1024 });
    private readonly ConcurrentDictionary<string, Lazy<Task<byte[]?>>> pending = new();
    private readonly SemaphoreSlim generationSlots = new(4);

    public async Task<byte[]?> SynthesizeAsync(string text, CancellationToken ct)
    {
        var apiKey = configuration["ElevenLabs:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey)) return null;
        var voice = Setting("VoiceId", "iSFxP4Z6YNcx9OXl62Ic");
        var model = Setting("Model", "eleven_v4_turbo");
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{voice}\n{model}\n{text}")));
        if (cache.TryGetValue<byte[]>(key, out var hit)) return hit;

        // Concurrent replays share one paid request. Disconnecting a player does not cancel other players.
        var work = pending.GetOrAdd(key, _ => new Lazy<Task<byte[]?>>(() => GenerateAsync(key, apiKey, voice, model, text)));
        return await work.Value.WaitAsync(ct);
    }

    private async Task<byte[]?> GenerateAsync(string key, string apiKey, string voice, string model, string text)
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var acquired = false;
        try
        {
            await generationSlots.WaitAsync(budget.Token);
            acquired = true;
            if (cache.TryGetValue<byte[]>(key, out var hit)) return hit;
            using var http = clients.CreateClient("ElevenLabs");
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://api.elevenlabs.io/v1/text-to-speech/{Uri.EscapeDataString(voice)}?output_format=mp3_44100_128");
            request.Headers.Add("xi-api-key", apiKey);
            request.Headers.Accept.ParseAdd("audio/mpeg");
            request.Content = JsonContent.Create(new { text, model_id = model });
            // Do not retry a paid synthesis: a lost response may already have been billed.
            using var response = await http.SendAsync(request, budget.Token);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType != "audio/mpeg")
            {
                logger.LogWarning("Examiner speech provider returned HTTP {StatusCode}.", (int)response.StatusCode);
                return null;
            }
            var audio = await response.Content.ReadAsByteArrayAsync(budget.Token);
            if (audio.Length is 0 or > 4 * 1024 * 1024) return null;
            cache.Set(key, audio, new MemoryCacheEntryOptions
            {
                Size = audio.Length,
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
            });
            return audio;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            logger.LogWarning("Examiner speech provider unavailable ({FailureType}).", ex.GetType().Name);
            return null;
        }
        finally
        {
            if (acquired) generationSlots.Release();
            pending.TryRemove(key, out _);
        }
    }

    private string Setting(string name, string fallback) =>
        string.IsNullOrWhiteSpace(configuration[$"ElevenLabs:{name}"]) ? fallback : configuration[$"ElevenLabs:{name}"]!.Trim();

    public void Dispose()
    {
        cache.Dispose();
        generationSlots.Dispose();
    }
}
