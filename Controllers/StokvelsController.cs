namespace RondiTrack.Controllers;

using Microsoft.AspNetCore.Mvc;
using RondiTrack.Data;
using RondiTrack.Models;
using RondiTrack.Mapping;
using RondiTrack.Services;
using RondiTrack.Extensions;

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
        return stokvel is null
            ? this.ToProblem(ServiceResultStatus.NotFound, "Stokvel not found.")
            : Ok(StokvelMapper.ToResponse(stokvel));
    }

    [HttpPost]
    public async Task<ActionResult<StokvelResponse>> Create(CreateStokvelRequest request)
    {
        try
        {
            var stokvel = new Stokvel(request.Name, request.ContributionAmount);
            await _store.AddStokvelAsync(stokvel);
            var response = StokvelMapper.ToResponse(stokvel);
            return CreatedAtAction(nameof(GetById), new { id = stokvel.Id }, response);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<StokvelResponse>> Update(Guid id, CreateStokvelRequest request)
    {
        var stokvel = await _store.GetStokvelByIdAsync(id);
        if (stokvel is null)
            return this.ToProblem(ServiceResultStatus.NotFound, "Stokvel not found.");

        try
        {
            stokvel.Rename(request.Name);
            stokvel.UpdateContributionAmount(request.ContributionAmount);
            return Ok(StokvelMapper.ToResponse(stokvel));
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _store.DeleteStokvelAsync(id);
        return deleted
            ? NoContent()
            : this.ToProblem(ServiceResultStatus.NotFound, "Stokvel not found.");
    }

    [HttpPost("{id}/members")]
    public async Task<IActionResult> AddMember(Guid id, AddMemberRequest request)
    {
        var result = await _membershipService.AddMemberAsync(id, request.UserId);

        return result.Status switch
        {
            ServiceResultStatus.Success => NoContent(),
            _ => this.ToProblem(result.Status, result.ErrorMessage)
        };
    }

    [HttpPost("{id}/contributions")]
    public async Task<IActionResult> RecordContribution(
        Guid id,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        ContributionRequest request)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            // Missing header — malformed request, 400.
            return Problem(detail: "An Idempotency-Key header is required.", statusCode: StatusCodes.Status400BadRequest);

        var result = await _contributionService.ExecuteAsync(id, idempotencyKey, request);

        return result.Status switch
        {
            ServiceResultStatus.Success => CreatedAtAction(nameof(GetById), new { id }, result.Data),
            _ => this.ToProblem(result.Status, result.ErrorMessage)
        };
    }
}