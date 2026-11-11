using System.Net.Http.Headers;
using System.Text.Json;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.VivaSessions;
using Microsoft.Extensions.Configuration;

namespace AIpoweredVivaExamSystem.Infrastructure.VivaSessions;

public sealed class OpenAiSpeechTranscriber(HttpClient http, IConfiguration configuration) : ISpeechTranscriber
{
    public async Task<string> TranscribeAsync(byte[] audioBytes, string fileName, string language, string? promptHint, CancellationToken ct)
    {
        var key = configuration["Ai:ApiKey"];
        var sttModel = configuration["Ai:SttModel"];
        if (string.IsNullOrWhiteSpace(sttModel))
            sttModel = "whisper-1";

        if (string.IsNullOrWhiteSpace(key))
            throw new ResourceConflictException("Chưa cấu hình khóa AI (Ai__ApiKey) trên server cho STT. Vui lòng bật nhận diện giọng nói trực tiếp trên trình duyệt hoặc nhập nội dung câu trả lời.");

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(audioBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "file", Path.GetFileName(string.IsNullOrWhiteSpace(fileName) ? "answer.webm" : fileName));
        form.Add(new StringContent(sttModel), "model");

        var normalizedLang = NormalizeLanguageCode(language);
        if (!string.IsNullOrEmpty(normalizedLang))
            form.Add(new StringContent(normalizedLang), "language");

        if (!string.IsNullOrWhiteSpace(promptHint))
            form.Add(new StringContent(promptHint.Trim()), "prompt");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/transcriptions")
        {
            Content = form
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

        try
        {
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                throw new ResourceConflictException($"Dịch vụ nhận diện giọng nói trả về mã lỗi HTTP {(int)response.StatusCode}.");

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var text = json.RootElement.GetProperty("text").GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(text))
                throw new ResourceConflictException("Không nhận diện được giọng nói trong bản ghi âm. Vui lòng nói rõ hơn hoặc kiểm tra lại micro.");

            return text;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ResourceConflictException("Yêu cầu nhận diện giọng nói bị quá thời gian chờ. Vui lòng thử lại.");
        }
        catch (HttpRequestException)
        {
            throw new ResourceConflictException("Không thể kết nối dịch vụ nhận diện giọng nói. Vui lòng thử lại.");
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new ResourceConflictException("Phản hồi từ dịch vụ nhận diện giọng nói không hợp lệ.");
        }
    }

    private static string NormalizeLanguageCode(string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return "vi";
        var prefix = language.Split('-', '_')[0].Trim().ToLowerInvariant();
        return prefix is "en" ? "en" : "vi";
    }
}

