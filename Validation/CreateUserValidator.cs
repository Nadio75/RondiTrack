// Validation/CreateUserRequestValidator.cs
namespace RondiTrack.Validation;

using FluentValidation;
using RondiTrack.Models;

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        // Shape only: is there actually a name, and is it a sane length?
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must be 100 characters or fewer.");

        // Shape only: is there a contact number at all, roughly the right length?
        // This is NOT checking whether the number is real or reachable — that's not a shape concern.
        RuleFor(x => x.ContactNumber)
            .NotEmpty().WithMessage("Contact number is required.")
            .Matches(@"^\d{10}$").WithMessage("Contact number must be exactly 10 digits.");
    }
}