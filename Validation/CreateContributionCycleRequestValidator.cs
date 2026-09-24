// Validation/CreateContributionCycleRequestValidator.cs
namespace RondiTrack.Validation;

using FluentValidation;
using RondiTrack.Models;

public class CreateContributionCycleRequestValidator : AbstractValidator<CreateContributionCycleRequest>
{
    public CreateContributionCycleRequestValidator()
    {
        RuleFor(x => x.Period)
            .NotEmpty().WithMessage("Period is required.")
            .Matches(@"^\d{4}-(0[1-9]|1[0-2])$").WithMessage("Period must be in the format YYYY-MM.");

        RuleFor(x => x.TargetAmount)
            .GreaterThan(0).WithMessage("Target amount must be greater than zero.");
    }
}