namespace AIpoweredVivaExamSystem.DataAccess.Common;

public sealed class DomainValidationException(string message) : Exception(message);
