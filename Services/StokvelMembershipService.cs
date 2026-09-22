// Services/StokvelMembershipService.cs
namespace RondiTrack.Services;

using RondiTrack.Data;

public class StokvelMembershipService
{
    private readonly IStokvelStore _stokvelStore;
    private readonly IStokvelStore _userStoreProxy; // see note below

    public StokvelMembershipService(IStokvelStore stokvelStore)
    {
        _stokvelStore = stokvelStore;
    }

    public async Task<ServiceResult<bool>> AddMemberAsync(Guid stokvelId, Guid userId)
    {
        // Step 1: does the stokvel exist?
        var stokvel = await _stokvelStore.GetStokvelByIdAsync(stokvelId);
        if (stokvel is null) return ServiceResult<bool>.NotFound("Stokvel not found.");

        // Step 2: does the user exist?
        var user = await _stokvelStore.GetUserByIdAsync(userId);
        if (user is null) return ServiceResult<bool>.NotFound("User not found.");

        // Step 3: the actual business decision — are they already a member?
        try
        {
            stokvel.AddMember(userId);
            return ServiceResult<bool>.Success(true);
        }
        catch (InvalidOperationException ex)
        {
            return ServiceResult<bool>.Conflict(ex.Message);
        }
    }
}