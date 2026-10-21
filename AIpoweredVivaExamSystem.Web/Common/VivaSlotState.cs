using AIpoweredVivaExamSystem.Application.VivaSessions;
using AIpoweredVivaExamSystem.Domain.Entities;

namespace AIpoweredVivaExamSystem.Web.Common;

// What the student's "Mở phiên thi" button should offer, mirroring the rule in VivaSessionStore.GetOrCreateAsync:
// a new session needs an ongoing exam and an open slot [start, end); an existing session can always be reopened,
// but the list does not say whether one exists, so after the slot the button stays as "Xem lại phiên thi".
public sealed record VivaSlotState(bool CanOpen, bool IsPrimary, string Label, string? Reason)
{
    public static VivaSlotState For(MyVivaExam exam, DateTimeOffset now)
    {
        if (exam.Status == ExamStatus.Cancelled)
            return new(false, false, "Mở phiên thi", "Kỳ thi đã bị hủy.");
        if (exam.ScheduledAt is not { } start || exam.EndAt is not { } end)
            return new(false, false, "Mở phiên thi", "Bạn chưa được xếp ca thi.");
        if (exam.Status == ExamStatus.Published)
            return new(false, false, "Mở phiên thi", $"Kỳ thi chưa bắt đầu. Ca của bạn: {start.ToLocalText()}.");
        if (exam.Status == ExamStatus.Ongoing && now < start)
            return new(false, false, "Mở phiên thi", $"Chưa đến giờ. Ca mở lúc {start.ToLocalText()}.");
        if (exam.Status == ExamStatus.Ongoing && now < end)
            return new(true, true, "Mở phiên thi", $"Ca đang mở đến {end.ToLocalText()}.");
        return new(true, false, "Xem lại phiên thi", "Ca thi đã kết thúc. Chỉ xem lại được phiên bạn đã làm.");
    }
}
