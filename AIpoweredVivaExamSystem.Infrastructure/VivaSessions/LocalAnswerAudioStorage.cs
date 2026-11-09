using AIpoweredVivaExamSystem.Application.VivaSessions;

namespace AIpoweredVivaExamSystem.Infrastructure.VivaSessions;

public sealed class LocalAnswerAudioStorage : IAnswerAudioStorage
{
    private readonly string _rootDirectory;

    public LocalAnswerAudioStorage(string? rootDirectory = null)
    {
        _rootDirectory = string.IsNullOrWhiteSpace(rootDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "App_Data", "viva-audios")
            : rootDirectory;
    }

    public async Task<string> SaveAudioAsync(Guid sessionId, Guid turnId, byte[] content, string mimeType, CancellationToken ct)
    {
        var ext = mimeType switch
        {
            "audio/wav" or "audio/wave" or "audio/x-wav" => ".wav",
            "audio/mp4" => ".mp4",
            "audio/mpeg" or "audio/mp3" => ".mp3",
            "audio/ogg" => ".ogg",
            _ => ".webm"
        };

        var sessionFolder = sessionId.ToString("N");
        var fileName = $"{turnId:N}{ext}";
        var directory = Path.Combine(_rootDirectory, sessionFolder);
        Directory.CreateDirectory(directory);

        var fullPath = Path.Combine(directory, fileName);
        await File.WriteAllBytesAsync(fullPath, content, ct);
        return $"viva-audios/{sessionFolder}/{fileName}";
    }
}

