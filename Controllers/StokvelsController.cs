namespace RondiTrack.Controllers;

using Microsoft.AspNetCore.Mvc;
using RondiTrack.Data;
using RondiTrack.Models;

[ApiController]
[Route("api/stokvels")]
public class StokvelsController : ControllerBase
{
    private readonly IStokvelStore _store;

    public StokvelsController(IStokvelStore store)
    {
        _store = store;
    }

    // GET /api/stokvels
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Stokvel>>> GetAll()
    {
        var stokvels = await _store.GetAllStokvelsAsync();
        return Ok(stokvels);
    }

    // GET /api/stokvels/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<Stokvel>> GetById(Guid id)
    {
        var stokvel = await _store.GetStokvelByIdAsync(id);
        return stokvel is null ? NotFound() : Ok(stokvel);
    }

    // POST /api/stokvels
    [HttpPost]
    public async Task<ActionResult<Stokvel>> Create(CreateStokvelRequest request)
    {
        try
        {
            // Constructor enforces: name not blank, contribution amount > 0.
            var stokvel = new Stokvel(request.Name, request.ContributionAmount);
            await _store.AddStokvelAsync(stokvel);

            return CreatedAtAction(nameof(GetById), new { id = stokvel.Id }, stokvel);
        }
        catch (ArgumentException ex)
        {
            // Covers both "blank name" AND "contribution <= 0" —
            // both are the constructor rejecting bad input, both are 400.
            return BadRequest(ex.Message);
        }
    }

    // PUT /api/stokvels/{id}
    // Purpose: update name and/or contribution amount.
    [HttpPut("{id}")]
    public async Task<ActionResult<Stokvel>> Update(Guid id, CreateStokvelRequest request)
    {
        var stokvel = await _store.GetStokvelByIdAsync(id);
        if (stokvel is null)
        {
            return NotFound();
        }

        try
        {
            stokvel.Rename(request.Name);
            stokvel.UpdateContributionAmount(request.ContributionAmount);
            return Ok(stokvel);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // DELETE /api/stokvels/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _store.DeleteStokvelAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    // POST /api/stokvels/{id}/members
    // Purpose: add an existing user to this stokvel.
    // Note the route: {id} here refers to the STOKVEL's id (nested resource,
    // per the "nested relationship" requirement in the brief). The user
    // being added is identified in the request BODY, not the URL, because
    // membership is being created (POST = create something) on the stokvel.
    [HttpPost("{id}/members")]
    public async Task<IActionResult> AddMember(Guid id, AddMemberRequest request)
    {
        var stokvel = await _store.GetStokvelByIdAsync(id);
        if (stokvel is null)
        {
            // The stokvel itself doesn't exist — 404.
            return NotFound("Stokvel not found.");
        }

        var user = await _store.GetUserByIdAsync(request.UserId);
        if (user is null)
        {
            // You can't add a user who doesn't exist — also 404, but for
            // a DIFFERENT reason. The message clarifies which one failed.
            return NotFound("User not found.");
        }

        try
        {
            // The "no duplicate members" rule lives on Stokvel.AddMember
            // itself (see the Models walkthrough) — the controller just
            // calls it and reacts.
            stokvel.AddMember(request.UserId);
            return NoContent(); // 204 — membership added, nothing to return
        }
        catch (InvalidOperationException ex)
        {
            // THIS is your "meaningful failure beyond not-found":
            // both resources exist, the request is well-formed, but the
            // OPERATION conflicts with the current state (already a member).
            // 409 Conflict is the semantically correct code for that —
            // not 400 (the request wasn't malformed) and not 404
            // (everything referenced does exist).
            return Conflict(ex.Message);
        }
    }

    // DELETE /api/stokvels/{id}/members/{userId}
    // Purpose: remove a member from a stokvel. Two ids in the route because
    // we're identifying a specific membership relationship, not a standalone
    // resource — this is the "nested relationship, resource-oriented" routing
    // the brief asks for.
    [HttpDelete("{id}/members/{userId}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId)
    {
        var stokvel = await _store.GetStokvelByIdAsync(id);
        if (stokvel is null)
        {
            return NotFound("Stokvel not found.");
        }

        try
        {
            stokvel.RemoveMember(userId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            // "This user isn't a member" — arguably could be 404 instead
            // of 409 here; both are defensible. 404 might read cleaner
            // since it's "that membership doesn't exist to remove."
            // Pick one and justify it in your README — this is exactly
            // the kind of judgment call the assignment wants you to make
            // and explain, not get "right" from a fixed answer key.
            return NotFound(ex.Message);
        }
    }
}