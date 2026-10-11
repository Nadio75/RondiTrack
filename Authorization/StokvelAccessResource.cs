namespace RondiTrack.Authorization;

public sealed record StokvelAccessResource(
    Guid StokvelId,
    string RequiredRole);