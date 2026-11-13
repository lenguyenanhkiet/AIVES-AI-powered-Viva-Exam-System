using AIpoweredVivaExamSystem.DataAccess.Entities;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Exams;

public sealed record SchedulePreview(string PreviewToken, IReadOnlyList<ScheduleSlot> Slots);
