// Exceptions/NotFoundException.cs
namespace RondiTrack.Exceptions;

// Thrown when a request references a resource — a stokvel, a user, a contribution cycle —
// that doesn't exist. Maps to 404.
public class NotFoundException : RondiTrackException
{
    public NotFoundException(string message) : base(message) { }
}