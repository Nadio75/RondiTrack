// Controllers/ContributionCyclesController.cs
namespace RondiTrack.Controllers;

using Microsoft.AspNetCore.Mvc;
using RondiTrack.Data;
using RondiTrack.Models;
using RondiTrack.Mapping;
using RondiTrack.Exceptions;

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
    public async Task<ActionResult<IEnumerable<ContributionCycleResponse>>> GetAll(Guid stokvelId)
    {
        var stokvel = await _stokvelStore.GetStokvelByIdAsync(stokvelId);
        if (stokvel is null) throw new NotFoundException("Stokvel not found.");

        var cycles = await _cycleStore.GetAllByStokvelAsync(stokvelId);
        return Ok(cycles.Select(ContributionCycleMapper.ToResponse));
    }

    [HttpGet("{cycleId}")]
    public async Task<ActionResult<ContributionCycleResponse>> GetById(Guid stokvelId, Guid cycleId)
    {
        var cycle = await _cycleStore.GetByIdAsync(cycleId);
        // Also checking StokvelId stops someone fetching a real cycle id through the wrong stokvel's route.
        if (cycle is null || cycle.StokvelId != stokvelId)
            throw new NotFoundException("Contribution cycle not found.");

        return Ok(ContributionCycleMapper.ToResponse(cycle));
    }

    [HttpPost]
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
    public async Task<IActionResult> Delete(Guid stokvelId, Guid cycleId)
    {
        var cycle = await _cycleStore.GetByIdAsync(cycleId);
        if (cycle is null || cycle.StokvelId != stokvelId)
            throw new NotFoundException("Contribution cycle not found.");

        await _cycleStore.DeleteAsync(cycleId);
        return NoContent();
    }
}