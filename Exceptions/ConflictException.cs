// Exceptions/ConflictException.cs
namespace RondiTrack.Exceptions;

// Thrown when a request is well-formed and references real resources, but contradicts a
// business fact RondiTrack already knows to be true — this user is already a member of
// this stokvel, this member already paid for this cycle. Maps to 409.
public class ConflictException : RondiTrackException
{
    public ConflictException(string message) : base(message) { }
}