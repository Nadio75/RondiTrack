// Services/ServiceResult.cs
namespace RondiTrack.Services;

// The outcome categories a service can hand back — controller turns this into the matching status code.
public enum ServiceResultStatus { Success, NotFound, Conflict, Unprocessable }

public class ServiceResult<T>
{
    public required ServiceResultStatus Status { get; init; }
    public T? Data { get; init; }
    public string? ErrorMessage { get; init; }

    // Shortcut for the happy path.
    public static ServiceResult<T> Success(T data) => new() { Status = ServiceResultStatus.Success, Data = data };
    public static ServiceResult<T> NotFound(string message) => new() { Status = ServiceResultStatus.NotFound, ErrorMessage = message };
    public static ServiceResult<T> Conflict(string message) => new() { Status = ServiceResultStatus.Conflict, ErrorMessage = message };
    public static ServiceResult<T> Unprocessable(string message) => new() { Status = ServiceResultStatus.Unprocessable, ErrorMessage = message };
}