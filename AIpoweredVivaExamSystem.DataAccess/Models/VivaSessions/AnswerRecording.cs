using AIpoweredVivaExamSystem.DataAccess.Common;

namespace AIpoweredVivaExamSystem.DataAccess.Models.VivaSessions;

public static class AnswerRecording
{
    public static string Validate(byte[] content, string? mimeType)
    {
        if (content.Length > 20 * 1024 * 1024)
            throw new DomainValidationException("File ghi âm vượt quá giới hạn 20 MB.");
        if (string.IsNullOrWhiteSpace(mimeType)) return "audio/webm";
        var normalized = mimeType.Split(';')[0].Trim().ToLowerInvariant();
        return normalized switch
        {
            "audio/webm" or "audio/wav" or "audio/wave" or "audio/x-wav" or "audio/mp4" or "audio/mpeg" or "audio/mp3" or "audio/ogg" => normalized,
            _ => throw new DomainValidationException("Định dạng âm thanh không hợp lệ (chỉ hỗ trợ webm, wav, mp4, mp3, ogg).")
        };
    }
}
