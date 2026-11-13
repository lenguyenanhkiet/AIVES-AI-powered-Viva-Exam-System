using AIpoweredVivaExamSystem.Domain.Common;

namespace AIpoweredVivaExamSystem.Application.Common;

public static class BusinessErrors
{
    public static bool IsRuleViolation(Exception exception) => exception is DomainValidationException;
}
