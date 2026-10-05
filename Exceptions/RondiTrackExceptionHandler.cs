// Exceptions/RondiTrackExceptionHandler.cs
namespace RondiTrack.Exceptions;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
    var correlationId = httpContext.TraceIdentifier;

    // --- Map unique constraint violations (Postgres 23505) to 409 ---
    if (exception is DbUpdateException dbEx)
    {
        var postgresEx = dbEx.InnerException as PostgresException
                      ?? dbEx.InnerException?.InnerException as PostgresException;

        if (postgresEx?.SqlState == PostgresErrorCodes.UniqueViolation) // "23505"
        {
            exception = new ConflictException(
                "A resource with the same unique key already exists.");
        }
    }

    var (statusCode, title) = exception switch
    {
        NotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
        ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
        IdempotencyConflictException => (StatusCodes.Status409Conflict, "Idempotency Key Conflict"),
        BusinessRuleViolationException => (StatusCodes.Status422UnprocessableEntity, "Unprocessable Entity"),
        _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
    };

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

    var json = System.Text.Json.JsonSerializer.Serialize(problemDetails);
    await httpContext.Response.WriteAsync(json, cancellationToken);

    return true;
}
}