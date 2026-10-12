using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RondiTrack.Domain;

namespace RondiTrack.Exceptions;

public class RondiTrackExceptionHandler : IExceptionHandler
{
    private readonly ILogger<RondiTrackExceptionHandler> _logger;

    public RondiTrackExceptionHandler(
        ILogger<RondiTrackExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var correlationId =
            Activity.Current?.TraceId.ToString()
            ?? httpContext.TraceIdentifier;

        if (exception is DbUpdateException dbEx)
        {
            var postgresEx =
                dbEx.InnerException as PostgresException
                ?? dbEx.InnerException?.InnerException
                    as PostgresException;

            if (postgresEx?.SqlState ==
                PostgresErrorCodes.UniqueViolation)
            {
                exception = new ConflictException(
                    "A resource with the same unique key already exists.");
            }
        }

        var (statusCode, title) = exception switch
        {
            NotFoundException =>
                (StatusCodes.Status404NotFound, "Not Found"),

            ConflictException =>
                (StatusCodes.Status409Conflict, "Conflict"),

            IdempotencyConflictException =>
                (StatusCodes.Status409Conflict,
                    "Idempotency Key Conflict"),

            BusinessRuleViolationException =>
                (StatusCodes.Status422UnprocessableEntity,
                    "Unprocessable Entity"),

            _ =>
                (StatusCodes.Status500InternalServerError,
                    "An unexpected error occurred.")
        };

        if (statusCode < 500)
        {
            _logger.LogInformation(
                "Request handled with status {StatusCode}, " +
                "exception type {ExceptionType}, " +
                "correlationId {CorrelationId}",
                statusCode,
                exception.GetType().Name,
                correlationId);
        }
        else
        {
            _logger.LogError(
                exception,
                "Unhandled request failure, " +
                "correlationId {CorrelationId}",
                correlationId);
        }

        var detail = statusCode >= 500
            ? "An unexpected error occurred. Please try again."
            : exception.Message;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };

        problemDetails.Extensions["correlationId"] =
            correlationId;

        httpContext.Response.StatusCode = statusCode;

await httpContext.Response.WriteAsJsonAsync(
    problemDetails,
    options: null,
    contentType: "application/problem+json",
    cancellationToken: cancellationToken);

return true;
    }
}