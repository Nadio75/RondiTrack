// Exceptions/RondiTrackExceptionHandler.cs
namespace RondiTrack.Exceptions;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

// Implements ASP.NET Core's built-in IExceptionHandler interface — this is the ONE place
// in the whole app that decides what an exception becomes on the wire. Nothing else should
// catch an exception and build a response by hand anymore.
public class RondiTrackExceptionHandler : IExceptionHandler
{
    private readonly ILogger<RondiTrackExceptionHandler> _logger;

    public RondiTrackExceptionHandler(ILogger<RondiTrackExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // ASP.NET Core already generates a unique id per request — reusing it as our
        // correlation id means we don't need a separate middleware just to invent one.
        var correlationId = httpContext.TraceIdentifier;

        // One switch expression maps every exception type to its status code and title —
        // this is the single source of truth the whole app relies on now.
        var (statusCode, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            IdempotencyConflictException => (StatusCodes.Status409Conflict, "Idempotency Key Conflict"),
            BusinessRuleViolationException => (StatusCodes.Status422UnprocessableEntity, "Unprocessable Entity"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
        };

        // Known domain failures are expected traffic, not bugs — log them as warnings with
        // just the message. Anything NOT in our hierarchy is genuinely unexpected, so it's
        // logged as an error with the full exception (stack trace included).
        if (exception is RondiTrackException)
        {
            _logger.LogWarning(
                "Handled {ExceptionType}: {Message} [correlationId={CorrelationId}]",
                exception.GetType().Name, exception.Message, correlationId);
        }
        else
        {
            _logger.LogError(
                exception,
                "Unhandled exception [correlationId={CorrelationId}]",
                correlationId);
        }

        // Never leak an unexpected exception's raw message to a caller — only our own
        // known exception types get their message surfaced. Anything else gets a generic detail.
        var detail = exception is RondiTrackException
            ? exception.Message
            : "An unexpected error occurred. Please try again.";

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Extensions = { ["correlationId"] = correlationId }
        };

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true; // tells ASP.NET Core: "handled, don't do anything further with this exception"
    }
}