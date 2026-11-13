using AIpoweredVivaExamSystem.DataAccess.Models.Common;
using AIpoweredVivaExamSystem.DataAccess.Entities;
using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Authentication;

public sealed record AccessOverview(IReadOnlyList<AccessUser> Users, IReadOnlyList<AccessSubject> Subjects,
    IReadOnlyList<SubjectAssignment> Assignments);
