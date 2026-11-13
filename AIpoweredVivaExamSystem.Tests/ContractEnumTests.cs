using AIpoweredVivaExamSystem.Application.Contracts;
using Xunit;
using DomainEnums = AIpoweredVivaExamSystem.Domain.Enums;
using DomainEntities = AIpoweredVivaExamSystem.Domain.Entities;

namespace AIpoweredVivaExamSystem.Tests;

public sealed class ContractEnumTests
{
    [Theory]
    [InlineData(typeof(AcademicStatus), typeof(DomainEnums.AcademicStatus))]
    [InlineData(typeof(BloomLevel), typeof(DomainEnums.BloomLevel))]
    [InlineData(typeof(QuestionDifficulty), typeof(DomainEnums.QuestionDifficulty))]
    [InlineData(typeof(QuestionSourceType), typeof(DomainEnums.QuestionSourceType))]
    [InlineData(typeof(QuestionStatus), typeof(DomainEnums.QuestionStatus))]
    [InlineData(typeof(AnswerType), typeof(DomainEnums.AnswerType))]
    [InlineData(typeof(UserRole), typeof(DomainEnums.UserRole))]
    [InlineData(typeof(UserStatus), typeof(DomainEnums.UserStatus))]
    [InlineData(typeof(ExamStatus), typeof(DomainEntities.ExamStatus))]
    public void Application_enum_matches_domain_values(Type contractType, Type domainType)
    {
        var contractValues = Enum.GetNames(contractType).Select(name => (name,
            Value: Convert.ToInt32(Enum.Parse(contractType, name)))).OrderBy(item => item.name);
        var domainValues = Enum.GetNames(domainType).Select(name => (name,
            Value: Convert.ToInt32(Enum.Parse(domainType, name)))).OrderBy(item => item.name);

        Assert.Equal(domainValues, contractValues);
    }
}
