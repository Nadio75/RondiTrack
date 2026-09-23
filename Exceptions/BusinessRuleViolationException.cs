// Exceptions/BusinessRuleViolationException.cs
namespace RondiTrack.Exceptions;

// Thrown when a request is syntactically well-formed (it already passed FluentValidation)
// but violates a domain rule that only makes sense once real data is involved — not just a
// shape check. Reserved for rules that can't be expressed as "is this field present/positive"
// alone. Maps to 422.
public class BusinessRuleViolationException : RondiTrackException
{
    public BusinessRuleViolationException(string message) : base(message) { }
}