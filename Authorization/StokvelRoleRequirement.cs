using Microsoft.AspNetCore.Authorization;

namespace RondiTrack.Authorization;

public sealed class StokvelRoleRequirement(string requiredRole)
    : IAuthorizationRequirement
{
    public string RequiredRole { get; } = requiredRole;
}