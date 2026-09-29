using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AIpoweredVivaExamSystem.Api.Middleware;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            ValidationException or DomainValidationException => (400, "Validation failed"),
            ResourceNotFoundException => (404, "Resource not found"),
            ResourceConflictException => (409, "Resource conflict"),
            _ => (500, "An unexpected error occurred")
        };
        if (status == 500)
            logger.LogError(exception, "Unhandled API error. TraceId: {TraceId}", context.TraceIdentifier);

        ProblemDetails problem = exception is ValidationException validation
            ? new ValidationProblemDetails(validation.Errors.GroupBy(x => x.PropertyName)
                .ToDictionary(x => x.Key, x => x.Select(e => e.ErrorMessage).Distinct().ToArray()))
            : new ProblemDetails();
        problem.Status = status;
        problem.Title = title;
        problem.Detail = status == 500 ? "Contact support with the traceId." : exception.Message;
        problem.Instance = context.Request.Path;
        problem.Extensions["traceId"] = context.TraceIdentifier;
        await Results.Problem(problem).ExecuteAsync(context);
        return true;
    }
}
