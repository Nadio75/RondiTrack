// Exceptions/IdempotencyConflictException.cs
namespace RondiTrack.Exceptions;

// Thrown specifically when an Idempotency-Key is reused with a different request payload
// than the one it was first associated with. Kept separate from ConflictException on
// purpose: this isn't a fact about stokvels or contributions at all — it's a violation of
// the retry-safety protocol itself. A client hitting this needs to understand "you reused a
// key incorrectly," not "this business action already happened," even though both currently
// map to the same 409 status code.
public class IdempotencyConflictException : RondiTrackException
{
    public IdempotencyConflictException(string message) : base(message) { }
}