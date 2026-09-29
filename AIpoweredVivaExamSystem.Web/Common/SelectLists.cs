using AIpoweredVivaExamSystem.Application.Subjects.DTOs;
using AIpoweredVivaExamSystem.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AIpoweredVivaExamSystem.Web.Common;

public static class SelectLists
{
    public static IEnumerable<SelectListItem> Statuses() =>
        Enum.GetValues<AcademicStatus>().Select(x => new SelectListItem(x.ToLabel(), x.ToString()));

    public static IEnumerable<SelectListItem> Subjects(IEnumerable<SubjectResponse> subjects) =>
        subjects.Select(x => new SelectListItem($"{x.Code} — {x.Name}", x.Id.ToString()));
}
