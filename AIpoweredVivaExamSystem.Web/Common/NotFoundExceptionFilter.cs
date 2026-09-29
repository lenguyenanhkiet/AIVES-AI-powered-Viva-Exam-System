using AIpoweredVivaExamSystem.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AIpoweredVivaExamSystem.Web.Common;

// A missing Subject/Topic from the business layer becomes a 404 page instead of an error page.
public sealed class NotFoundExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not ResourceNotFoundException)
            return;
        context.Result = new NotFoundResult();
        context.ExceptionHandled = true;
    }
}
