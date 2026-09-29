using AIpoweredVivaExamSystem.Application.Questions.DTOs;
using AIpoweredVivaExamSystem.Application.Subjects.DTOs;

namespace AIpoweredVivaExamSystem.Web.Models;

public sealed class DashboardViewModel
{
    public int SubjectCount { get; init; }
    public int TopicCount { get; init; }
    public int QuestionCount { get; init; }
    public int DraftCount { get; init; }
    public int ApprovedCount { get; init; }
    public int RubricCount { get; init; }
    public required IReadOnlyList<SubjectResponse> RecentSubjects { get; init; }
    public required IReadOnlyList<QuestionResponse> PendingQuestions { get; init; }
}
