using RondiTrack.Models.Dtos;
using RondiTrack.Models;
namespace RondiTrack.Controllers;

using Microsoft.AspNetCore.Mvc;
using RondiTrack.Domain;
using RondiTrack.Mapping;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IStokvelStore _store;

    public UsersController(IStokvelStore store) => _store = store;

    [HttpGet]
    [EndpointSummary("List all users")]
    [EndpointDescription("""
        Returns every user in the system, in the order they were created.

        **Guarantees:** always succeeds; returns an empty array if there are no users.

        **Does not:** support paging, filtering or sorting. The whole list is returned every time.

        **Example response (200):**

            [
              { "id": "3f2b8c1e-5d4a-4c7b-9a10-1e2f3a4b5c6d", "name": "Thabo Nkosi", "contactNumber": "0821234567" }
            ]
        """)]
    [ProducesResponseType<IEnumerable<UserResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<UserResponse>>> GetAll()
    {
        var users = await _store.GetAllUsersAsync();
        return Ok(users.Select(UserMapper.ToResponse));
    }

    [HttpGet("{id}")]
    [EndpointSummary("Get one user by id")]
    [EndpointDescription("""
        Returns the user with the given id.

        **Guarantees:** a `200` always contains the full user. An unknown id always gives `404`.

        **Errors:** `400` if `id` is not a valid GUID; `404` if no user has that id.

        **Example response (200):**

            { "id": "3f2b8c1e-5d4a-4c7b-9a10-1e2f3a4b5c6d", "name": "Thabo Nkosi", "contactNumber": "0821234567" }
        """)]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<UserResponse>> GetById(Guid id)
    {
        var user = await _store.GetUserByIdReadOnlyAsync(id);        if (user is null) throw new NotFoundException("User not found.");
        return Ok(UserMapper.ToResponse(user));
    }

    [HttpPost]
    [EndpointSummary("Create a user")]
    [EndpointDescription("""
        Creates a new user. The server generates the `id`.

        **Rules:** `name` is required and at most 100 characters. `contactNumber` is required and must be
        exactly 10 digits (no spaces, dashes or `+`).

        **Guarantees:** on `201`, the user exists and a `Location` header points to it.

        **Does not:** check that the contact number is real or reachable, and does not stop two users
        sharing the same contact number.

        **Errors:** `400` if any rule above is broken, or the body is missing or is not valid JSON.

        **Example request:**

            { "name": "Thabo Nkosi", "contactNumber": "0821234567" }

        **Example response (201):**

            { "id": "3f2b8c1e-5d4a-4c7b-9a10-1e2f3a4b5c6d", "name": "Thabo Nkosi", "contactNumber": "0821234567" }
        """)]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request)
    {
        User user;
        try
        {
            user = new User(request.Name, request.ContactNumber);
        }
        catch (ArgumentException ex)
        {
            // FluentValidation already caught shape problems before this ran — anything
            // still caught here is a rule the entity enforces that validation doesn't
            // duplicate. Translated into the hierarchy, not formatted here.
            throw new BusinessRuleViolationException(ex.Message);
        }

        await _store.AddUserAsync(user);
        var response = UserMapper.ToResponse(user);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, response);
    }

    [HttpPut("{id}")]
    [EndpointSummary("Replace a user's name and contact number")]
    [EndpointDescription("""
        Replaces **both** fields of an existing user. This is a full update, not a partial one: send
        `name` and `contactNumber` every time. The `id` never changes.

        **Rules:** the same as creating a user (`name` required, max 100 characters; `contactNumber`
        exactly 10 digits).

        **Errors:** `400` if `id` is not a valid GUID or a rule is broken (checked before the lookup, so an
        invalid body for an unknown id gives `400`, not `404`); `404` if no user has that id.

        **Example request:**

            { "name": "Thabo M. Nkosi", "contactNumber": "0821234567" }

        **Example response (200):**

            { "id": "3f2b8c1e-5d4a-4c7b-9a10-1e2f3a4b5c6d", "name": "Thabo M. Nkosi", "contactNumber": "0821234567" }
        """)]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<UserResponse>> Update(Guid id, CreateUserRequest request)
    {
        var user = await _store.GetUserByIdAsync(id);
        if (user is null) throw new NotFoundException("User not found.");

        try
        {
            user.Rename(request.Name);
            user.UpdateContactNumber(request.ContactNumber);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleViolationException(ex.Message);
        }

        return Ok(UserMapper.ToResponse(user));
    }

    [HttpDelete("{id}")]
    [EndpointSummary("Delete a user")]
    [EndpointDescription("""
        Deletes the user with the given id. Returns `204` with no body.

        **Does not:** remove the user from any stokvel they have joined. Their id stays in that
        stokvel's member list, and later contributions from that id fail with `404 User not found`.

        **Errors:** `400` if `id` is not a valid GUID; `404` if no user has that id (so deleting the
        same user twice gives `204` and then `404`).
        """)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _store.DeleteUserAsync(id);
        if (!deleted) throw new NotFoundException("User not found.");
        return NoContent();
    }
}

