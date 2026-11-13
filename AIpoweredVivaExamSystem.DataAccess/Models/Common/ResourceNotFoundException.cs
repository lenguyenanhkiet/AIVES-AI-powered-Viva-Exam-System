namespace AIpoweredVivaExamSystem.DataAccess.Models.Common;

public sealed class ResourceNotFoundException(string message) : Exception(message);
