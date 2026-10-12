
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RondiTrack.Exceptions;

public sealed class CorrelationProblemDetailsFilter : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(
        ResultExecutingContext context,
        ResultExecutionDelegate next)
    {
        if (context.Result is ObjectResult { Value: ProblemDetails problem })
        {
            problem.Extensions["correlationId"] =
                Activity.Current?.TraceId.ToString()
                ?? context.HttpContext.TraceIdentifier;
        }

        await next();
    }
}
