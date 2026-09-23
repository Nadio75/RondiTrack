namespace RondiTrack.Services;

using RondiTrack.Data;
using RondiTrack.Exceptions;

public class StokvelMembershipService
{
    private readonly IStokvelStore _stokvelStore;

    public StokvelMembershipService(IStokvelStore stokvelStore) => _stokvelStore = stokvelStore;

    // No more ServiceResult<T> — this either completes silently (success) or throws.
    // The controller no longer has anything to switch on.
    public async Task AddMemberAsync(Guid stokvelId, Guid userId)
    {
        var stokvel = await _stokvelStore.GetStokvelByIdAsync(stokvelId);
        if (stokvel is null) throw new NotFoundException("Stokvel not found.");

        var user = await _stokvelStore.GetUserByIdAsync(userId);
        if (user is null) throw new NotFoundException("User not found.");

        try
        {
            stokvel.AddMember(userId);
        }
        catch (InvalidOperationException ex)
        {
            // Translating the entity's own exception type into our domain hierarchy —
            // this is the one place that conversion needs to happen.
            throw new ConflictException(ex.Message);
        }
    }
}