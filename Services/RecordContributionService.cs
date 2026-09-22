// Services/RecordContributionService.cs
namespace RondiTrack.Services;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RondiTrack.Data;
using RondiTrack.Models;
using RondiTrack.Mapping;

public class RecordContributionService
{
    private readonly IStokvelStore _stokvelStore;
    private readonly IContributionStore _contributionStore;
    private readonly IIdempotencyStore _idempotencyStore;

    public RecordContributionService(
        IStokvelStore stokvelStore,
        IContributionStore contributionStore,
        IIdempotencyStore idempotencyStore)
    {
        _stokvelStore = stokvelStore;
        _contributionStore = contributionStore;
        _idempotencyStore = idempotencyStore;
    }

    public async Task<ServiceResult<ContributionResponse>> ExecuteAsync(
        Guid stokvelId, string idempotencyKey, ContributionRequest request)
    {
        // Step 1: turn this request's actual content into a fingerprint we can compare later.
        var requestHash = Hash(request);

        // Step 2: has this exact key been used before?
        var existing = await _idempotencyStore.FindAsync(idempotencyKey);
        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
            {
                // Same key, different payload — this is a tampering/bug case, reject it.
                return ServiceResult<ContributionResponse>.Conflict(
                    "This Idempotency-Key was already used with a different request.");
            }

            // Same key, same payload — replay the exact original response, do nothing new.
            var replayed = JsonSerializer.Deserialize<ContributionResponse>(existing.ResponseBodyJson)!;
            return ServiceResult<ContributionResponse>.Success(replayed);
        }

        // Step 3: does the stokvel exist?
        var stokvel = await _stokvelStore.GetStokvelByIdAsync(stokvelId);
        if (stokvel is null) return ServiceResult<ContributionResponse>.NotFound("Stokvel not found.");

        // Step 4: does the user exist, and are they actually a member?
        var user = await _stokvelStore.GetUserByIdAsync(request.UserId);
        if (user is null) return ServiceResult<ContributionResponse>.NotFound("User not found.");
        if (!stokvel.MemberIds.Contains(request.UserId))
            return ServiceResult<ContributionResponse>.NotFound("This user is not a member of this stokvel.");

        // Step 5: the real business rule — have they already paid for this cycle?
        var duplicate = await _contributionStore.FindAsync(stokvelId, request.UserId, request.CycleMonth);
        if (duplicate is not null)
            return ServiceResult<ContributionResponse>.Conflict(
                "This member has already paid for this cycle.");

        // Step 6: amount is well-formed JSON but might still violate a business rule (422, not 400).
        Contribution contribution;
        try
        {
            contribution = new Contribution(stokvelId, request.UserId, request.Amount, request.CycleMonth);
        }
        catch (ArgumentException ex)
        {
            return ServiceResult<ContributionResponse>.Unprocessable(ex.Message);
        }

        // Step 7: save the contribution itself.
        await _contributionStore.AddAsync(contribution);
        var response = ContributionMapper.ToResponse(contribution);

        // Step 8: remember this key + response, so a retry returns this exact result instead of recording twice.
        await _idempotencyStore.SaveAsync(new IdempotencyRecord
        {
            Key = idempotencyKey,
            RequestHash = requestHash,
            ResponseStatusCode = StatusCodes.Status201Created,
            ResponseBodyJson = JsonSerializer.Serialize(response)
        });

        return ServiceResult<ContributionResponse>.Success(response);
    }

    // Turns the request body into a short fixed fingerprint, so we can detect "same key, different payload."
    private static string Hash(ContributionRequest request)
    {
        var json = JsonSerializer.Serialize(request);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }
}