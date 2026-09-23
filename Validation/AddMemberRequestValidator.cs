// Validation/AddMemberRequestValidator.cs
namespace RondiTrack.Validation;

using FluentValidation;
using RondiTrack.Models;

public class AddMemberRequestValidator : AbstractValidator<AddMemberRequest>
{
    public AddMemberRequestValidator()
    {
        // Shape only: is this actually a real, non-empty Guid? Whether that user ID
        // corresponds to a real user is a business-layer question, not a shape one.
        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty).WithMessage("UserId must be a valid, non-empty GUID.");
    }
}