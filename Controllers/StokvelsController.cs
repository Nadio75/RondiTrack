namespace RondiTrack.Controllers;

using Microsoft.AspNetCore.Mvc;
using RondiTrack.Data;
using RondiTrack.Models;
using RondiTrack.Mapping;
using RondiTrack.Services;
using RondiTrack.Exceptions;

[ApiController]
[Route("api/stokvels")]
public class StokvelsController : ControllerBase
{
        private readonly IStokvelStore _store;
    private readonly StokvelMembershipService _membershipService;
    private readonly RecordContributionService _contributionService;
    private readonly PayoutService _payoutService;

    public StokvelsController(
        IStokvelStore store,
        StokvelMembershipService membershipService,
        RecordContributionService contributionService,
        PayoutService payoutService)
    {
        _store = store;
        _membershipService = membershipService;
        _contributionService = contributionService;
        _payoutService = payoutService;
    }

    [HttpGet]
    [EndpointSummary("List all stokvels")]
    [EndpointDescription("""
        Returns every stokvel, in the order they were created. Each entry shows a `memberCount`
        instead of the member list itself.

        **Guarantees:** always succeeds; returns an empty array if there are no stokvels.

        **Does not:** support paging, filtering or sorting, and does not list who the members are.

        **Example response (200):**

            [
              {
                "id": "9c1d7e40-2b6a-4f3e-8d55-7a0b1c2d3e4f",
                "name": "Soweto Savings Club",
                "contributionAmount": 500,
                "memberCount": 2
              }
            ]
        """)]
    [ProducesResponseType<IEnumerable<StokvelResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<StokvelResponse>>> GetAll()
    {
        var stokvels = await _store.GetAllStokvelsAsync();
        return Ok(stokvels.Select(StokvelMapper.ToResponse));
    }

    [HttpGet("{id}")]
    [EndpointSummary("Get one stokvel by id")]
    [EndpointDescription("""
        Returns the stokvel with the given id.

        **Guarantees:** a `200` always contains the full stokvel. An unknown id always gives `404`.

        **Errors:** `400` if `id` is not a valid GUID; `404` if no stokvel has that id.

        **Example response (200):**

            {
              "id": "9c1d7e40-2b6a-4f3e-8d55-7a0b1c2d3e4f",
              "name": "Soweto Savings Club",
              "contributionAmount": 500,
              "memberCount": 2
            }
        """)]
    [ProducesResponseType<StokvelResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<StokvelResponse>> GetById(Guid id)
    {
        var stokvel = await _store.GetStokvelByIdAsync(id);
        if (stokvel is null) throw new NotFoundException("Stokvel not found.");
        return Ok(StokvelMapper.ToResponse(stokvel));
    }

    [HttpPost]
    [EndpointSummary("Create a stokvel")]
    [EndpointDescription("""
        Creates a new stokvel with no members. The server generates the `id`.

        **Rules:** `name` is required and at most 100 characters. `contributionAmount` must be greater
        than zero.

        **Guarantees:** on `201`, the stokvel exists with `memberCount` 0, and a `Location` header
        points to it.

        **Does not:** stop two stokvels sharing the same name.

        **Errors:** `400` if any rule above is broken, or the body is missing or is not valid JSON.

        **Example request:**

            { "name": "Soweto Savings Club", "contributionAmount": 500 }

        **Example response (201):**

            {
              "id": "9c1d7e40-2b6a-4f3e-8d55-7a0b1c2d3e4f",
              "name": "Soweto Savings Club",
              "contributionAmount": 500,
              "memberCount": 0
            }
        """)]
    [ProducesResponseType<StokvelResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<StokvelResponse>> Create(CreateStokvelRequest request)
    {
        Stokvel stokvel;
        try
        {
            stokvel = new Stokvel(request.Name, request.ContributionAmount);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleViolationException(ex.Message);
        }

        await _store.AddStokvelAsync(stokvel);
        var response = StokvelMapper.ToResponse(stokvel);
        return CreatedAtAction(nameof(GetById), new { id = stokvel.Id }, response);
    }

    [HttpPut("{id}")]
    [EndpointSummary("Replace a stokvel's name and contribution amount")]
    [EndpointDescription("""
        Replaces **both** fields of an existing stokvel. This is a full update, not a partial one:
        send `name` and `contributionAmount` every time. The `id` and the member list are not changed.

        **Rules:** the same as creating a stokvel.

        **Errors:** `400` if `id` is not a valid GUID or a rule is broken (checked before the lookup, so an
        invalid body for an unknown id gives `400`, not `404`); `404` if no stokvel has that id.

        **Example request:**

            { "name": "Soweto Savings Club", "contributionAmount": 750 }

        **Example response (200):**

            {
              "id": "9c1d7e40-2b6a-4f3e-8d55-7a0b1c2d3e4f",
              "name": "Soweto Savings Club",
              "contributionAmount": 750,
              "memberCount": 2
            }
        """)]
    [ProducesResponseType<StokvelResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<StokvelResponse>> Update(Guid id, CreateStokvelRequest request)
    {
        var stokvel = await _store.GetStokvelByIdAsync(id);
        if (stokvel is null) throw new NotFoundException("Stokvel not found.");

        try
        {
            stokvel.Rename(request.Name);
            stokvel.UpdateContributionAmount(request.ContributionAmount);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleViolationException(ex.Message);
        }

        return Ok(StokvelMapper.ToResponse(stokvel));
    }

    [HttpDelete("{id}")]
    [EndpointSummary("Delete a stokvel")]
    [EndpointDescription("""
        Deletes the stokvel with the given id. Returns `204` with no body.

        **Does not:** delete the stokvel's contribution cycles or recorded contributions. They are left
        behind but can no longer be reached, because every cycle route needs an existing stokvel.

        **Errors:** `400` if `id` is not a valid GUID; `404` if no stokvel has that id (so deleting the
        same stokvel twice gives `204` and then `404`).
        """)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _store.DeleteStokvelAsync(id);
        if (!deleted) throw new NotFoundException("Stokvel not found.");
        return NoContent();
    }

    [HttpPost("{id}/members")]
    [EndpointSummary("Add a user to a stokvel as a member")]
    [EndpointDescription("""
        Makes an existing user a member of an existing stokvel. Returns `204` with no body; call
        `GET /api/stokvels/{id}` and check `memberCount` to see the result.

        **Checks, in this order:** the body is valid, the stokvel exists, the user exists, the user is
        not already a member.

        **Errors:** `400` if `id` is not a valid GUID, `userId` is missing or the empty GUID, or the body
        is not valid JSON; `404` if the stokvel or the user does not exist; `409` if the user is already
        a member of this stokvel.

        **Example request:**

            { "userId": "3f2b8c1e-5d4a-4c7b-9a10-1e2f3a4b5c6d" }
        """)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> AddMember(Guid id, AddMemberRequest request)
    {
        // No switch, no ToProblem — if this throws, the handler answers. If it doesn't, it worked.
        await _membershipService.AddMemberAsync(id, request.UserId);
        return NoContent();
    }

    [HttpPost("{id}/contributions")]
    [EndpointSummary("Record a member's contribution for a cycle (idempotent)")]
    [EndpointDescription("""
        Records that a member has paid into one contribution cycle of this stokvel.

        **Idempotency-Key header (required):** send a unique value (for example a GUID) with every
        new contribution. Scalar lists the header as optional because of how it is declared, but a
        request without it is rejected with `422`.
        - Same key, same body: nothing new is recorded. You get back the original `201` response, identical to the first.
        - Same key, different body: rejected with `409`. Use a new key for a new contribution.
        - Only successful requests are remembered, so retrying after a failure runs the request again.
        - The key is compared with the request body only, not the stokvel id in the URL.
        - Keys are kept in memory: they are forgotten when the application restarts and never expire.

        **Checks, in this order:** the body is valid, the header is present, the key has not been used
        before (a replay is answered here), the stokvel exists, the user exists, the user is a member
        of this stokvel, the cycle exists and belongs to this stokvel, this member has not already paid
        this cycle.

        **Does not:** check that `amount` matches the stokvel's contribution amount or the cycle's
        target. Any amount above zero is accepted.

        **Errors:** `400` if `id` is not a valid GUID, `userId` or `contributionCycleId` is missing or
        the empty GUID, `amount` is zero or less, or the body is not valid JSON;
        `404` if the stokvel, the user, or the cycle does not exist, if the user is not a member of
        this stokvel, or if the cycle belongs to a different stokvel; `409` if this member has already
        paid this cycle, or the Idempotency-Key was already used with a different body;
        `422` if the Idempotency-Key header is missing or blank.

        **Location header:** points to the stokvel (`GET /api/stokvels/{id}`), because contributions
        have no endpoint of their own to fetch.

        **Example request** (with header `Idempotency-Key: 6b0f5c1e-9f0a-4d2b-8a55-3c7d1e2f4a60`):

            {
              "userId": "3f2b8c1e-5d4a-4c7b-9a10-1e2f3a4b5c6d",
              "amount": 500,
              "contributionCycleId": "b7e4a2d1-6c3f-4e58-9b20-5d1a0c9e8f77"
            }

        **Example response (201):**

            {
              "id": "e2a9c4b6-1d8f-4a73-b5c0-9f6e3d2a1b48",
              "stokvelId": "9c1d7e40-2b6a-4f3e-8d55-7a0b1c2d3e4f",
              "userId": "3f2b8c1e-5d4a-4c7b-9a10-1e2f3a4b5c6d",
              "amount": 500,
              "contributionCycleId": "b7e4a2d1-6c3f-4e58-9b20-5d1a0c9e8f77",
              "createdAt": "2026-09-28T09:15:00Z"
            }
        """)]
    [ProducesResponseType<ContributionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> RecordContribution(
        Guid id,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        ContributionRequest request)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new BusinessRuleViolationException("An Idempotency-Key header is required.");

        var response = await _contributionService.ExecuteAsync(id, idempotencyKey, request);
        return CreatedAtAction(nameof(GetById), new { id }, response);
    }

        [HttpPost("{id}/cycles/{cycleId}/payout")]
    [EndpointSummary("Process the next payout for a contribution cycle")]
    [EndpointDescription("""
        Determines the next eligible recipient — the member who joined this stokvel earliest
        and has not yet received a payout from it — records a Payout for them, and marks the
        cycle as paid out. Both writes happen inside one database transaction: either both
        succeed, or neither is kept.

        **Errors:** `404` if the stokvel or cycle does not exist, or the cycle does not belong
        to this stokvel; `409` if the cycle has already been paid out, or every member has
        already received a payout from this stokvel.
        """)]
    [ProducesResponseType<RondiTrack.Models.Dtos.PayoutResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> ProcessPayout(Guid id, Guid cycleId)
    {
        var payout = await _payoutService.ProcessAsync(id, cycleId);
        return CreatedAtAction(nameof(GetById), new { id }, RondiTrack.Mapping.PayoutMapper.ToResponse(payout));
    }
}