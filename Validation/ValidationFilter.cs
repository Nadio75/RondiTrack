using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RondiTrack.Validation;

public class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
                continue;

            var validatorType = typeof(IValidator<>)
                .MakeGenericType(argument.GetType());

            if (context.HttpContext.RequestServices
                .GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validationContext =
                new ValidationContext<object>(argument);

            var result = await validator.ValidateAsync(
                validationContext,
                context.HttpContext.RequestAborted);

            if (!result.IsValid)
            {
                foreach (var error in result.Errors)
                {
                    var propertyName = error.PropertyName;

                    if (!string.IsNullOrEmpty(propertyName))
                    {
                        propertyName = JsonNamingPolicy.CamelCase
                            .ConvertName(propertyName);
                    }

                    context.ModelState.AddModelError(
                        propertyName,
                        error.ErrorMessage);
                }

                var problemDetails =
                    new ValidationProblemDetails(context.ModelState)
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "One or more validation errors occurred."
                    };

                context.Result = new ObjectResult(problemDetails)
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    ContentTypes = { "application/problem+json" }
                };

                return;
            }
        }

        await next();
    }
}