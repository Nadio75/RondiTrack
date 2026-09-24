// Services/RecordContributionService.cs
namespace RondiTrack.Services;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RondiTrack.Data;
using RondiTrack.Models;
using RondiTrack.Mapping;
using RondiTrack.Exceptions;

public class RecordContributionService
{
    private readonly IStokvelStore _stokvelStore;
    private readonly IContributionStore _contributionStore;
    private readonly IContributionCycleStore _cycleStore;
    private readonly IIdempotencyStore _idempotencyStore;

    public RecordContributionService(
        IStokvelStore stokvelStore,
        IContributionStore contributionStore,
        IContributionCycleStore cycleStore,
        IIdempotencyStore idempotencyStore)
    {
        _stokvelStore = stokvelStore;
        _contributionStore = contributionStore;
        _cycleStore = cycleStore;
        _idempotencyStore = idempotencyStore;
    }

    public async Task<ContributionResponse> ExecuteAsync(
        Guid stokvelId, string idempotencyKey, ContributionRequest request)
    {
        var requestHash = Hash(request);

        var existing = await _idempotencyStore.FindAsync(idempotencyKey);
        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
                throw new IdempotencyConflictException(
                    "This Idempotency-Key was already used with a different request.");

            return JsonSerializer.Deserialize<ContributionResponse>(existing.ResponseBodyJson)!;
        }

        var stokvel = await _stokvelStore.GetStokvelByIdAsync(stokvelId);
        if (stokvel is null) throw new NotFoundException("Stokvel not found.");

        var user = await _stokvelStore.GetUserByIdAsync(request.UserId);
        if (user is null) throw new NotFoundException("User not found.");
        if (!stokvel.MemberIds.Contains(request.UserId))
            throw new NotFoundException("This user is not a member of this stokvel.");

        // New check: the cycle being paid into has to actually exist, and has to belong
        // to THIS stokvel — not just any stokvel's cycle.
        var cycle = await _cycleStore.GetByIdAsync(request.ContributionCycleId);
        if (cycle is null || cycle.StokvelId != stokvelId)
            throw new NotFoundException("This contribution cycle does not exist for this stokvel.");

        var duplicate = await _contributionStore.FindAsync(stokvelId, request.UserId, request.ContributionCycleId);
        if (duplicate is not null)
            throw new ConflictException("This member has already paid for this cycle.");

        Contribution contribution;
        try
        {
            contribution = new Contribution(stokvelId, request.UserId, request.Amount, request.ContributionCycleId);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleViolationException(ex.Message);
        }

        await _contributionStore.AddAsync(contribution);
        var response = ContributionMapper.ToResponse(contribution);

        await _idempotencyStore.SaveAsync(new IdempotencyRecord
        {
            Key = idempotencyKey,
            RequestHash = requestHash,
            ResponseStatusCode = StatusCodes.Status201Created,
            ResponseBodyJson = JsonSerializer.Serialize(response)
        });

        return response;
    }

    private static string Hash(ContributionRequest request)
    {
        var json = JsonSerializer.Serialize(request);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }
}