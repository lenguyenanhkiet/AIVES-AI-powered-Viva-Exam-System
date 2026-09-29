using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;
using Xunit;

namespace AIpoweredVivaExamSystem.Tests;

public class SubjectTopicDomainTests
{
    [Fact]
    public void Subject_update_trims_and_replaces_fields()
    {
        var subject = new Subject("PRN222", "Programming with C#");
        subject.Update(" PRN231 ", " Web API ", "Desc", AcademicStatus.Inactive);
        Assert.Equal("PRN231", subject.Code);
        Assert.Equal("Web API", subject.Name);
        Assert.Equal("Desc", subject.Description);
        Assert.Equal(AcademicStatus.Inactive, subject.Status);
    }

    [Theory]
    [InlineData("", "Name")]
    [InlineData("CODE", " ")]
    [InlineData("123456789012345678901234567890123456789012345678901", "Name")]
    public void Invalid_subject_update_does_not_mutate(string code, string name)
    {
        var subject = new Subject("PRN222", "Programming with C#");
        Assert.Throws<DomainValidationException>(() => subject.Update(code, name, null, AcademicStatus.Active));
        Assert.Equal("PRN222", subject.Code);
        Assert.Equal("Programming with C#", subject.Name);
    }

    [Fact]
    public void Topic_update_keeps_subject_and_rejects_invalid_status()
    {
        var subjectId = Guid.NewGuid();
        var topic = new Topic(subjectId, "LINQ");
        topic.Update("EF Core", null, AcademicStatus.Inactive);
        Assert.Equal(subjectId, topic.SubjectId);
        Assert.Equal("EF Core", topic.Name);
        Assert.Throws<DomainValidationException>(() => topic.Update("X", null, (AcademicStatus)99));
        Assert.Equal("EF Core", topic.Name);
    }

    [Fact]
    public void Topic_requires_subject() =>
        Assert.Throws<DomainValidationException>(() => new Topic(Guid.Empty, "LINQ"));
}
