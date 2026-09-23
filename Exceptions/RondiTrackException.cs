// Exceptions/RondiTrackException.cs
namespace RondiTrack.Exceptions;

// The common base every RondiTrack-specific failure inherits from. Having this lets the
// centralized handler (next phase) catch "anything from our own domain" in one place,
// separately from truly unexpected exceptions it didn't design for.
public abstract class RondiTrackException : Exception
{
    protected RondiTrackException(string message) : base(message) { }
}