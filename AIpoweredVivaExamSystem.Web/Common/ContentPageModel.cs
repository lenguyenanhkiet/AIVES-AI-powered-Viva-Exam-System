using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AIpoweredVivaExamSystem.Web.Common;

// Typed partials keep the existing form field names and validation keys when a POST redisplays a page.
public abstract class ContentPageModel : PageModel
{
    public string ContentPartial { get; private set; } = string.Empty;
    public object? ContentModel { get; private set; }

    protected PageResult RenderContent(string partial, object? model)
    {
        ContentPartial = partial;
        ContentModel = model;
        return Page();
    }
}
