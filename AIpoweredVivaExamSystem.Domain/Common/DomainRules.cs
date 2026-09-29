namespace AIpoweredVivaExamSystem.Domain.Common;

public static class DomainRules
{
    public static string RequiredText(string? value, string field, int maxLength = int.MaxValue)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maxLength)
            throw new DomainValidationException($"{field} is required and must not exceed {maxLength} characters.");
        return value.Trim();
    }

    public static void RequiredId(Guid value, string field)
    {
        if (value == Guid.Empty)
            throw new DomainValidationException($"{field} must not be empty.");
    }

    public static void DefinedEnum<T>(T value, string field) where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
            throw new DomainValidationException($"{field} is invalid.");
    }
}
