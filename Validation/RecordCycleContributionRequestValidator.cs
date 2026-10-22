
using FluentValidation;
using RondiTrack.Models;

namespace RondiTrack.Validation;

public class RecordCycleContributionRequestValidator
    : AbstractValidator<RecordCycleContributionRequest>
{
    public RecordCycleContributionRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty)
            .WithMessage("UserId must be a valid, non-empty GUID.");

        RuleFor(x => x.AmountInCents)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than zero.");
    }
}
