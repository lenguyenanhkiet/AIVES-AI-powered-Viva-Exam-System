namespace AIpoweredVivaExamSystem.DataAccess.Models.Common;

public sealed class ResourceConflictException(string message) : Exception(message);
