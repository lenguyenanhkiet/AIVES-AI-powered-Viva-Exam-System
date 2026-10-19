using System.ComponentModel.DataAnnotations;
using AIpoweredVivaExamSystem.Application.QuestionBank;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AIpoweredVivaExamSystem.Web.ViewModels;

public sealed class ImportQuestionsViewModel
{
    public IFormFile? File { get; set; }
    public string? Token { get; set; }
    public ImportPreview? Preview { get; set; }
}
public sealed class DocumentsViewModel
{
    [Required] public Guid? SubjectId { get; set; }
    public IFormFile? File { get; set; }
    public IReadOnlyList<CourseDocument> Documents { get; set; } = [];
    public IEnumerable<SelectListItem> Subjects { get; set; } = [];
}
public sealed class GenerateQuestionsViewModel
{
    [Required] public Guid DocumentId { get; set; }
    public Guid? TopicId { get; set; }
    [Required, StringLength(1000, MinimumLength = 3)] public string Instruction { get; set; } = "";
    [EnumDataType(typeof(BloomLevel))] public BloomLevel BloomLevel { get; set; } = BloomLevel.Understand;
    [EnumDataType(typeof(QuestionDifficulty))] public QuestionDifficulty Difficulty { get; set; } = QuestionDifficulty.Medium;
    [Range(1, 10)] public int Count { get; set; } = 3;
    public string FileName { get; set; } = "";
    public IEnumerable<SelectListItem> Topics { get; set; } = [];
}
