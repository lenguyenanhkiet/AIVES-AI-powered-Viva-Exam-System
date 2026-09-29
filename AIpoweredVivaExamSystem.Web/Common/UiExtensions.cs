using System.Globalization;
using AIpoweredVivaExamSystem.Domain.Enums;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace AIpoweredVivaExamSystem.Web.Common;

public static class UiExtensions
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public static string ToLocalText(this DateTimeOffset value) =>
        value.ToOffset(VietnamOffset).ToString("dd/MM/yyyy HH:mm");

    public static string ToLabel(this AcademicStatus status) => status switch
    {
        AcademicStatus.Active => "Đang dùng",
        AcademicStatus.Inactive => "Ngừng dùng",
        _ => status.ToString()
    };

    public static string ToLabel(this BloomLevel level) => level switch
    {
        BloomLevel.Remember => "Nhớ",
        BloomLevel.Understand => "Hiểu",
        BloomLevel.Apply => "Vận dụng",
        BloomLevel.Analyze => "Phân tích",
        _ => level.ToString()
    };

    public static string ToHint(this BloomLevel level) => level switch
    {
        BloomLevel.Remember => "Nhắc lại khái niệm, định nghĩa",
        BloomLevel.Understand => "Giải thích bằng lời của mình",
        BloomLevel.Apply => "Áp dụng vào tình huống cụ thể",
        BloomLevel.Analyze => "So sánh, mổ xẻ, lập luận",
        _ => string.Empty
    };

    public static string ToLabel(this QuestionDifficulty difficulty) => difficulty switch
    {
        QuestionDifficulty.Easy => "Dễ",
        QuestionDifficulty.Medium => "Trung bình",
        QuestionDifficulty.Hard => "Khó",
        _ => difficulty.ToString()
    };

    public static string ToLabel(this QuestionStatus status) => status switch
    {
        QuestionStatus.Draft => "Nháp",
        QuestionStatus.Approved => "Đã duyệt",
        QuestionStatus.Rejected => "Bị từ chối",
        QuestionStatus.Archived => "Lưu trữ",
        _ => status.ToString()
    };

    public static string ToLabel(this QuestionSourceType source) => source switch
    {
        QuestionSourceType.Manual => "Soạn thủ công",
        QuestionSourceType.AiGenerated => "AI sinh",
        QuestionSourceType.DocumentGenerated => "Sinh từ tài liệu",
        _ => source.ToString()
    };

    public static string ToScore(this decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    public static void Toast(this ITempDataDictionary tempData, string message, bool success = true)
    {
        tempData["Toast"] = message;
        tempData["ToastType"] = success ? "success" : "error";
    }

    // Business-layer validators are the source of truth; surface their errors on the matching form fields.
    public static void AddErrors(this ModelStateDictionary modelState, ValidationException exception)
    {
        foreach (var error in exception.Errors)
            modelState.AddModelError(error.PropertyName, error.ErrorMessage);
    }
}
