namespace AIpoweredVivaExamSystem.DataAccess.Models.Rubrics;

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
