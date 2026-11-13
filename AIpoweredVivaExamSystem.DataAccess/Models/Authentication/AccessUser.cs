using AIpoweredVivaExamSystem.DataAccess.Models.Common;
using AIpoweredVivaExamSystem.DataAccess.Entities;
using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Authentication;

public sealed record AccessUser(Guid Id, string Email, string FullName, UserRole Role);
