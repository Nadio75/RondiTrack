namespace RondiTrack.Controllers;

using Microsoft.AspNetCore.Mvc;
using RondiTrack.Data;
using RondiTrack.Models;

// [ApiController] switches on some automatic behaviors for us:
// - automatic 400 responses if the request body doesn't match the expected shape
// - automatic binding of route/query/body parameters without extra attributes
[ApiController]
// This sets the BASE route for every action below: everything here lives
// under /api/users. Individual [Http...] attributes add onto this.
[Route("api/users")]
public class UsersController : ControllerBase
{
    // We depend on the INTERFACE, not the concrete InMemoryStokvelStore class.
    // ASP.NET Core's DI container looks at Program.cs, sees we registered
    // IStokvelStore -> InMemoryStokvelStore, and hands us that instance here.
    private readonly IStokvelStore _store;

    public UsersController(IStokvelStore store)
    {
        _store = store;
    }

    // GET /api/users
    // Purpose: list everyone. There's no real failure case here beyond
    // "the list might be empty," which is still a 200 with an empty array —
    // an empty list is not an error.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<User>>> GetAll()
    {
        var users = await _store.GetAllUsersAsync();
        return Ok(users); // 200 OK, with the list as the response body
    }

    // GET /api/users/{id}
    // Purpose: fetch one specific user by their Guid.
    [HttpGet("{id}")]
    public async Task<ActionResult<User>> GetById(Guid id)
    {
        var user = await _store.GetUserByIdAsync(id);

        // The ternary here reads as: "if user is null, return 404 Not Found;
        // otherwise, return 200 OK with the user in the body."
        // This is exactly the "does the status code reflect what actually
        // happened" requirement — we don't return 200 with a null body,
        // and we don't throw an unhandled exception either.
        return user is null ? NotFound() : Ok(user);
    }

    // POST /api/users
    // Purpose: create a new user.
    [HttpPost]
    public async Task<ActionResult<User>> Create(CreateUserRequest request)
    {
        try
        {
            // The actual validation (empty name, empty contact number) lives
            // INSIDE the User constructor — the controller doesn't duplicate
            // that logic, it just tries to construct the object and reacts
            // to failure.
            var user = new User(request.Name, request.ContactNumber);
            await _store.AddUserAsync(user);

            // CreatedAtAction builds the correct 201 response:
            // - status code 201
            // - a Location header pointing at GET /api/users/{id} for this new user
            // - the created user as the response body
            // This is the "proper" way to respond to a successful POST per
            // REST convention — not just Ok(user).
            return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
        }
        catch (ArgumentException ex)
        {
            // The User constructor throws ArgumentException for bad input
            // (blank name, blank contact number). We catch it HERE, at the
            // boundary between "domain logic" and "HTTP," and translate it
            // into the right status code — this is the 400 Bad Request case:
            // the request itself was malformed/invalid, not "not found."
            return BadRequest(ex.Message);
        }
    }

    // PUT /api/users/{id}
    // Purpose: update an existing user's details (full replace of name + contact).
    [HttpPut("{id}")]
    public async Task<ActionResult<User>> Update(Guid id, CreateUserRequest request)
    {
        var user = await _store.GetUserByIdAsync(id);
        if (user is null)
        {
            // Can't update something that doesn't exist — 404, not a
            // silent no-op or a misleading 200.
            return NotFound();
        }

        try
        {
            // Again: the validation rule (non-blank name/contact) lives on
            // the entity's own methods, not duplicated here.
            user.Rename(request.Name);
            user.UpdateContactNumber(request.ContactNumber);
            return Ok(user); // 200 OK — update succeeded, here's the new state
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // DELETE /api/users/{id}
    // Purpose: remove a user entirely.
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _store.DeleteUserAsync(id);

        // DeleteUserAsync returns false if nothing matched that id —
        // that's a 404 (nothing there to delete), not a 204.
        // If it DID find and remove something, 204 No Content is correct:
        // the operation succeeded, and there's nothing meaningful to send back.
        return deleted ? NoContent() : NotFound();
    }
}