using AIpoweredVivaExamSystem.Application.VivaSessions;
using AIpoweredVivaExamSystem.Application.Contracts;

namespace AIpoweredVivaExamSystem.Web.Common;

// What the student's "Mở phiên thi" button should offer, mirroring the rule in VivaSessionStore.GetOrCreateAsync:
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
        if (exam.HasSession)
            return exam.Status == ExamStatus.Ongoing && now < end
                ? new(true, true, "Tiếp tục phiên thi", $"Ca kết thúc lúc {end.ToLocalText()}.")
                : new(true, false, "Xem lại phiên thi", "Bạn có thể xem lại phiên đã làm.");
        if (exam.Status != ExamStatus.Ongoing || now >= end)
            return new(false, false, "Ca thi đã kết thúc", "Bạn chưa mở phiên trong khung giờ đã xếp.");
        if (exam.EntryOpensAt is not { } opens || exam.EntryClosesAt is not { } closes || now >= closes)
            return new(false, false, "Đã khóa vào thi", "Đã hết thời gian mở phiên mới.");
        if (now < opens)
            return new(false, false, "Mở phiên thi", $"Chưa đến giờ. Ca mở lúc {opens.ToLocalText()}.");
        return new(true, true, "Mở phiên thi", $"Hạn mở phiên: {closes.ToLocalText()}.");
    }
}
