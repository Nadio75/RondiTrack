// Validation/ValidationFilter.cs
namespace RondiTrack.Validation;

using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

public class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Look at every argument the model binder produced for this action (route params,
        // query params, and — the one we actually care about — the request body DTO).
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null) continue;

            // Ask DI: "is there an IValidator<T> registered for this argument's exact type?"
            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
                continue; // no validator for this type (e.g. a Guid route param) — nothing to check

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(validationContext);

            if (!result.IsValid)
            {
                foreach (var error in result.Errors)
                    context.ModelState.AddModelError(error.PropertyName, error.ErrorMessage);

                // Reuses ASP.NET Core's built-in ValidationProblemDetails shape — already
                // problem+json, consistent with everything AddProblemDetails() already covers.
                var problemDetails = new ValidationProblemDetails(context.ModelState)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "One or more validation errors occurred."
                };

                context.Result = new ObjectResult(problemDetails)
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    ContentTypes = { "application/problem+json" }
                };
                return; // stop here — the controller action never runs
            }
        }

        await next(); // everything validated cleanly, let the request continue
    }
}