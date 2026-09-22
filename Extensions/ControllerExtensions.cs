// Extensions/ControllerExtensions.cs
namespace RondiTrack.Extensions;

using Microsoft.AspNetCore.Mvc;
using RondiTrack.Services;

public static class ControllerExtensions
{
    // Turns a ServiceResult's failure status into the matching Problem Details response.
    public static ObjectResult ToProblem(this ControllerBase controller, ServiceResultStatus status, string? message)
    {
        var statusCode = status switch
        {
            ServiceResultStatus.NotFound => StatusCodes.Status404NotFound,
            ServiceResultStatus.Conflict => StatusCodes.Status409Conflict,
            ServiceResultStatus.Unprocessable => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status500InternalServerError
        };

        return controller.Problem(detail: message, statusCode: statusCode);
    }
}