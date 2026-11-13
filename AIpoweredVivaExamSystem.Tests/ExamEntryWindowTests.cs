using AIpoweredVivaExamSystem.DataAccess.Entities;
using Xunit;

namespace AIpoweredVivaExamSystem.Tests;

public sealed class ExamEntryWindowTests
{
    [Theory]
    [InlineData(0, 5, 5, 10)]
    [InlineData(8, 0, 8, 13)]
    [InlineData(28, 0, 28, 30)]
    public void EntryWindowUsesLaterStartAndRespectsSlotEnd(
        int actualStart, int scheduled, int opens, int closes)
    {
        var start = new DateTimeOffset(2026, 11, 13, 13, 0, 0, TimeSpan.Zero);
        var exam = new Exam(Guid.NewGuid(), Guid.NewGuid(), "Entry window",
            start, start.AddHours(2), 30, 1, 0);
        exam.Publish();
        exam.Start(start.AddMinutes(actualStart));

        Assert.Equal((start.AddMinutes(opens), start.AddMinutes(closes)),
            exam.EntryWindow(start.AddMinutes(scheduled)));
    }

    [Fact]
    public void StartingAfterTheSlotDoesNotReopenIt()
    {
        var start = new DateTimeOffset(2026, 11, 13, 13, 0, 0, TimeSpan.Zero);
        var exam = new Exam(Guid.NewGuid(), Guid.NewGuid(), "Expired slot",
            start, start.AddHours(2), 30, 1, 0);
        exam.Publish();
        exam.Start(start.AddMinutes(30));

        Assert.Null(exam.EntryWindow(start));
    }
}
