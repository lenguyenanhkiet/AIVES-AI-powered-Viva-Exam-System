using AIpoweredVivaExamSystem.DataAccess.Enums;
using AIpoweredVivaExamSystem.DataAccess.Models.Subjects;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AIpoweredVivaExamSystem.Presentation.Common;

public static class SelectLists
{
    public static IEnumerable<SelectListItem> Statuses() =>
        Enum.GetValues<AcademicStatus>().Select(x => new SelectListItem(x.ToLabel(), x.ToString()));

    public static IEnumerable<SelectListItem> Subjects(IEnumerable<SubjectResponse> subjects) =>
        subjects.Select(x => new SelectListItem($"{x.Code} — {x.Name}", x.Id.ToString()));
}
