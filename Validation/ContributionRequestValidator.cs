// Validation/ContributionRequestValidator.cs
namespace RondiTrack.Validation;

using FluentValidation;
using RondiTrack.Models;

public class ContributionRequestValidator : AbstractValidator<ContributionRequest>
{
    public ContributionRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty).WithMessage("UserId must be a valid, non-empty GUID.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than zero.");

        // Shape only: is CycleMonth present and does it look like "2026-09"?
        // Whether that cycle actually exists is a business-layer question — coming
        // back once ContributionCycle replaces this raw string.
        RuleFor(x => x.CycleMonth)
            .NotEmpty().WithMessage("CycleMonth is required.")
            .Matches(@"^\d{4}-(0[1-9]|1[0-2])$").WithMessage("CycleMonth must be in the format YYYY-MM.");
    }
}