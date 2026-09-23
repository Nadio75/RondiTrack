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

    public StokvelsController(
        IStokvelStore store,
        StokvelMembershipService membershipService,
        RecordContributionService contributionService)
    {
        _store = store;
        _membershipService = membershipService;
        _contributionService = contributionService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<StokvelResponse>>> GetAll()
    {
        var stokvels = await _store.GetAllStokvelsAsync();
        return Ok(stokvels.Select(StokvelMapper.ToResponse));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<StokvelResponse>> GetById(Guid id)
    {
        var stokvel = await _store.GetStokvelByIdAsync(id);
        if (stokvel is null) throw new NotFoundException("Stokvel not found.");
        return Ok(StokvelMapper.ToResponse(stokvel));
    }

    [HttpPost]
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
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _store.DeleteStokvelAsync(id);
        if (!deleted) throw new NotFoundException("Stokvel not found.");
        return NoContent();
    }

    [HttpPost("{id}/members")]
    public async Task<IActionResult> AddMember(Guid id, AddMemberRequest request)
    {
        // No switch, no ToProblem — if this throws, the handler answers. If it doesn't, it worked.
        await _membershipService.AddMemberAsync(id, request.UserId);
        return NoContent();
    }

    [HttpPost("{id}/contributions")]
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
}