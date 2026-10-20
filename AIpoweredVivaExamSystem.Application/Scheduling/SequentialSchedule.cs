using System.Security.Cryptography;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Entities;

namespace AIpoweredVivaExamSystem.Application.Scheduling;

public static class SequentialSchedule
{
    public static IReadOnlyList<ScheduleSlot> Generate(DateTimeOffset startAt, DateTimeOffset endAt,
        int durationMinutes, IReadOnlyList<ScheduleEnrollment> roster)
    {
        if (roster.Count == 0)
            throw new DomainValidationException("Danh sách sinh viên trống. Hãy thêm sinh viên trước khi xếp lịch.");
        if (durationMinutes <= 0 || startAt >= endAt ||
            (endAt - startAt).Ticks / TimeSpan.TicksPerMinute < (long)durationMinutes * roster.Count)
            throw new DomainValidationException("Cửa sổ kỳ thi không đủ thời gian cho toàn bộ sinh viên.");

        var ordered = roster.OrderBy(x => x.StudentId).ToArray();
        return ordered.Select((x, i) =>
        {
            var start = startAt.ToUniversalTime().AddMinutes((long)i * durationMinutes);
            return new ScheduleSlot(x.EnrollmentId, start, start.AddMinutes(durationMinutes));
        }).ToArray();
    }

    public static SchedulePreview Preview(Exam exam, IReadOnlyList<ScheduleEnrollment> roster)
    {
        var slots = Generate(exam.StartAt, exam.EndAt, exam.DurationMinutes, roster);
        var versions = roster.ToDictionary(x => x.EnrollmentId, x => x.RowVersion);
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(exam.Id.ToByteArray());
            writer.Write(exam.RowVersion.Length);
            writer.Write(exam.RowVersion);
            writer.Write(slots.Count);
            foreach (var slot in slots)
            {
                writer.Write(slot.EnrollmentId.ToByteArray());
                writer.Write(versions[slot.EnrollmentId].Length);
                writer.Write(versions[slot.EnrollmentId]);
                writer.Write(slot.StartAt.UtcTicks);
                writer.Write(slot.EndAt.UtcTicks);
            }
        }
        return new(Convert.ToHexString(SHA256.HashData(stream.ToArray())), slots);
    }
}
