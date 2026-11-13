using AIpoweredVivaExamSystem.DataAccess.Models.Common;
using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.Business.VivaSessions;

public interface IExaminerSpeechSynthesizer
{
    Task<byte[]?> SynthesizeAsync(string text, CancellationToken ct);
}
