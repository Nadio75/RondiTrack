// Validation/CreateStokvelRequestValidator.cs
namespace RondiTrack.Validation;

using FluentValidation;
using RondiTrack.Models;

public class CreateStokvelRequestValidator : AbstractValidator<CreateStokvelRequest>
{
    public CreateStokvelRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must be 100 characters or fewer.");

        // Shape only: is the amount a positive number? Whether it matches a real contribution
        // schedule, or whether the stokvel already exists, is not this validator's job.
        RuleFor(x => x.ContributionAmount)
            .GreaterThan(0).WithMessage("Contribution amount must be greater than zero.");
    }
}