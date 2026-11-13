using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Domain.Common;
using Xunit;

namespace AIpoweredVivaExamSystem.Tests;

public sealed class BusinessErrorsTests
{
    [Fact]
    public void Recognizes_domain_validation_without_exposing_domain_type_to_web()
    {
        Assert.True(BusinessErrors.IsRuleViolation(new DomainValidationException("Invalid state")));
        Assert.False(BusinessErrors.IsRuleViolation(new InvalidOperationException("Failure")));
    }
}
