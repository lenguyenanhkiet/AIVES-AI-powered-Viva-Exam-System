namespace AIpoweredVivaExamSystem.Web.Models;

// Current filters are kept in RouteValues so page links preserve them.
public sealed record PagerModel(int Page, int PageSize, int TotalCount, IDictionary<string, string?> RouteValues)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public int From => TotalCount == 0 ? 0 : (Page - 1) * PageSize + 1;
    public int To => Math.Min(Page * PageSize, TotalCount);

    public IDictionary<string, string?> For(int page) =>
        new Dictionary<string, string?>(RouteValues) { ["page"] = page.ToString() };
}
