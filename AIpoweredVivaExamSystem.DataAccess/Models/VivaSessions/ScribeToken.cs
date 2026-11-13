using AIpoweredVivaExamSystem.DataAccess.Models.Common;

namespace AIpoweredVivaExamSystem.DataAccess.Models.VivaSessions;

public sealed record ScribeToken(string Token, string ModelId, string LanguageCode);
