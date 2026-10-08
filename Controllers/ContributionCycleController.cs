using RondiTrack.Models.Dtos;
using RondiTrack.Models;
// Controllers/ContributionCyclesController.cs
namespace RondiTrack.Controllers;

using Microsoft.AspNetCore.Mvc;
using RondiTrack.Domain;
using RondiTrack.Mapping;

// Nested under a stokvel, since a cycle only ever makes sense in the context of one:
// /api/stokvels/{stokvelId}/cycles
[ApiController]
[Route("api/stokvels/{stokvelId}/cycles")]
public class ContributionCyclesController : ControllerBase
{
    private readonly IStokvelStore _stokvelStore;
    private readonly IContributionCycleStore _cycleStore;

    public ContributionCyclesController(IStokvelStore stokvelStore, IContributionCycleStore cycleStore)
    {
        _stokvelStore = stokvelStore;
        _cycleStore = cycleStore;
    }

    [HttpGet]
    [EndpointSummary("List a stokvel's contribution cycles")]
    [EndpointDescription("""
        Returns every contribution cycle that belongs to the given stokvel, in the order they were
        created. Cycles of other stokvels are never included.

        **Guarantees:** a `200` always contains only this stokvel's cycles. It is an empty array if the
        stokvel has no cycles yet.

        **Does not:** support paging, filtering or sorting, and does not include the contributions
        recorded against each cycle.

        **Errors:** `400` if `stokvelId` is not a valid GUID; `404` if no stokvel has that id.

        **Example response (200):**

            [
              {
                "id": "b7e4a2d1-6c3f-4e58-9b20-5d1a0c9e8f77",
                "stokvelId": "9c1d7e40-2b6a-4f3e-8d55-7a0b1c2d3e4f",
                "period": "2026-09",
                "targetAmount": 1000,
                "createdAt": "2026-09-01T08:00:00Z"
              }
            ]
        """)]
    [ProducesResponseType<IEnumerable<ContributionCycleResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<IEnumerable<ContributionCycleResponse>>> GetAll(Guid stokvelId)
    {
        var stokvel = await _stokvelStore.GetStokvelByIdAsync(stokvelId);
        if (stokvel is null) throw new NotFoundException("Stokvel not found.");

        var cycles = await _cycleStore.GetAllByStokvelAsync(stokvelId);
        return Ok(cycles.Select(ContributionCycleMapper.ToResponse));
    }

    [HttpGet("{cycleId}")]
    [EndpointSummary("Get one contribution cycle by id")]
    [EndpointDescription("""
        Returns the contribution cycle with the given id, as long as it belongs to the stokvel in the URL.

        **Guarantees:** a `200` always contains the full cycle. A cycle id that exists but belongs to a
        different stokvel gives `404`, the same as an id that does not exist at all.

        **Errors:** `400` if `stokvelId` or `cycleId` is not a valid GUID; `404` if the cycle does not
        exist or does not belong to this stokvel. The stokvel itself is not looked up, so a stokvel id
        that does not exist also gives `404`, but with the cycle message.

        **Example response (200):**

            {
              "id": "b7e4a2d1-6c3f-4e58-9b20-5d1a0c9e8f77",
              "stokvelId": "9c1d7e40-2b6a-4f3e-8d55-7a0b1c2d3e4f",
              "period": "2026-09",
              "targetAmount": 1000,
              "createdAt": "2026-09-01T08:00:00Z"
            }
        """)]
    [ProducesResponseType<ContributionCycleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ContributionCycleResponse>> GetById(Guid stokvelId, Guid cycleId)
    {
        var cycle = await _cycleStore.GetByIdReadOnlyAsync(cycleId);        // Also checking StokvelId stops someone fetching a real cycle id through the wrong stokvel's route.
        if (cycle is null || cycle.StokvelId != stokvelId)
            throw new NotFoundException("Contribution cycle not found.");

        return Ok(ContributionCycleMapper.ToResponse(cycle));
    }

    [HttpPost]
    [EndpointSummary("Create a contribution cycle for a stokvel")]
    [EndpointDescription("""
        Creates a new contribution cycle (one month) for the stokvel in the URL. The server generates the
        `id` and `createdAt`. The stokvel comes from the URL, not the body.

        **Rules:** `period` is required and must be in the format `YYYY-MM` (for example `2026-09`; the
        month must be `01` to `12`). `targetAmount` must be greater than zero. A stokvel can have only
        one cycle per period.

        **Checks, in this order:** the body is valid, the stokvel exists, the stokvel does not already
        have a cycle for this period.

        **Guarantees:** on `201`, the cycle exists and a `Location` header points to it
        (`GET /api/stokvels/{stokvelId}/cycles/{cycleId}`).

        **Does not:** check that `targetAmount` matches the stokvel's contribution amount, or that the
        period is current or in the future. Past periods are accepted.

        **Errors:** `400` if `stokvelId` is not a valid GUID, a rule above is broken, or the body is
        missing or is not valid JSON; `404` if no stokvel has that id; `409` if the stokvel already has
        a cycle for this period.

        **Example request:**

            { "period": "2026-09", "targetAmount": 1000 }

        **Example response (201):**

            {
              "id": "b7e4a2d1-6c3f-4e58-9b20-5d1a0c9e8f77",
              "stokvelId": "9c1d7e40-2b6a-4f3e-8d55-7a0b1c2d3e4f",
              "period": "2026-09",
              "targetAmount": 1000,
              "createdAt": "2026-09-01T08:00:00Z"
            }
        """)]
    [ProducesResponseType<ContributionCycleResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<ContributionCycleResponse>> Create(Guid stokvelId, CreateContributionCycleRequest request)
    {
        var stokvel = await _stokvelStore.GetStokvelByIdAsync(stokvelId);
        if (stokvel is null) throw new NotFoundException("Stokvel not found.");

        // A single lookup-and-reject, not a multi-fact decision — this is why it's allowed
        // to live directly in the controller instead of needing a service method.
        var existing = await _cycleStore.FindByStokvelAndPeriodAsync(stokvelId, request.Period);
        if (existing is not null)
            throw new ConflictException("A contribution cycle for this period already exists for this stokvel.");

        ContributionCycle cycle;
        try
        {
            cycle = new ContributionCycle(stokvelId, request.Period, request.TargetAmount);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleViolationException(ex.Message);
        }

        await _cycleStore.AddAsync(cycle);
        var response = ContributionCycleMapper.ToResponse(cycle);
        return CreatedAtAction(nameof(GetById), new { stokvelId, cycleId = cycle.Id }, response);
    }

    [HttpPut("{cycleId}")]
    [EndpointSummary("Replace a contribution cycle's period and target amount")]
    [EndpointDescription("""
        Replaces **both** fields of an existing cycle. This is a full update, not a partial one: send
        `period` and `targetAmount` every time. The `id`, `stokvelId` and `createdAt` are not changed.

        **Rules:** the same as creating a cycle.

        **Does not:** check for a duplicate period. Unlike creating a cycle, updating a cycle to a
        period that another cycle of the same stokvel already uses is accepted, and the stokvel then
        has two cycles for that period. It also does not change contributions already recorded
        against the cycle.

        **Errors:** `400` if `stokvelId` or `cycleId` is not a valid GUID or a rule is broken (checked
        before the lookup, so an invalid body for an unknown cycle gives `400`, not `404`); `404` if the
        cycle does not exist or does not belong to this stokvel.

        **Example request:**

            { "period": "2026-10", "targetAmount": 1200 }

        **Example response (200):**

            {
              "id": "b7e4a2d1-6c3f-4e58-9b20-5d1a0c9e8f77",
              "stokvelId": "9c1d7e40-2b6a-4f3e-8d55-7a0b1c2d3e4f",
              "period": "2026-10",
              "targetAmount": 1200,
              "createdAt": "2026-09-01T08:00:00Z"
            }
        """)]
    [ProducesResponseType<ContributionCycleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ContributionCycleResponse>> Update(Guid stokvelId, Guid cycleId, CreateContributionCycleRequest request)
    {
        var cycle = await _cycleStore.GetByIdAsync(cycleId);
        if (cycle is null || cycle.StokvelId != stokvelId)
            throw new NotFoundException("Contribution cycle not found.");

        try
        {
            cycle.UpdatePeriod(request.Period);
            cycle.UpdateTargetAmount(request.TargetAmount);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleViolationException(ex.Message);
        }

        return Ok(ContributionCycleMapper.ToResponse(cycle));
    }

    [HttpDelete("{cycleId}")]
    [EndpointSummary("Delete a contribution cycle")]
    [EndpointDescription("""
        Deletes the contribution cycle with the given id, as long as it belongs to the stokvel in the URL.
        Returns `204` with no body.

        **Does not:** delete the contributions already recorded against the cycle. They are left behind.
        Once the cycle is gone, no new contribution can be recorded against it (that gives `404`).

        **Errors:** `400` if `stokvelId` or `cycleId` is not a valid GUID; `404` if the cycle does not
        exist or does not belong to this stokvel (so deleting the same cycle twice gives `204` and then
        `404`).
        """)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Delete(Guid stokvelId, Guid cycleId)
    {
        var cycle = await _cycleStore.GetByIdReadOnlyAsync(cycleId);        if (cycle is null || cycle.StokvelId != stokvelId)
            throw new NotFoundException("Contribution cycle not found.");

        await _cycleStore.DeleteAsync(cycleId);
        return NoContent();
    }
    
}

