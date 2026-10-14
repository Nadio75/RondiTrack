
namespace RondiTrack.Tests.TestSupport;

/// <summary>
/// A clock that tests can set to any UTC instant.
/// It lets us test monthly closing deadlines
/// without waiting for real calendar dates.
/// </summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public ManualTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow.ToUniversalTime();
    }

    public override DateTimeOffset GetUtcNow()
    {
        return _utcNow;
    }

    public void SetUtcNow(DateTimeOffset utcNow)
    {
        _utcNow = utcNow.ToUniversalTime();
    }
}
