namespace RondiTrack.Controllers;

using Microsoft.AspNetCore.Mvc;
using RondiTrack.Data;
using RondiTrack.Models;
using RondiTrack.Mapping;
using RondiTrack.Extensions;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IStokvelStore _store;

    public UsersController(IStokvelStore store) => _store = store;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserResponse>>> GetAll()
    {
        var users = await _store.GetAllUsersAsync();
        return Ok(users.Select(UserMapper.ToResponse));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserResponse>> GetById(Guid id)
    {
        var user = await _store.GetUserByIdAsync(id);
        // Was: NotFound() — now returns a proper Problem Details body instead of an empty 404.
        return user is null
            ? this.ToProblem(RondiTrack.Services.ServiceResultStatus.NotFound, "User not found.")
            : Ok(UserMapper.ToResponse(user));
    }

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request)
    {
        try
        {
            var user = new User(request.Name, request.ContactNumber);
            await _store.AddUserAsync(user);
            var response = UserMapper.ToResponse(user);
            return CreatedAtAction(nameof(GetById), new { id = user.Id }, response);
        }
        catch (ArgumentException ex)
        {
            // Malformed/invalid input on create — this is the 400 case.
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<UserResponse>> Update(Guid id, CreateUserRequest request)
    {
        var user = await _store.GetUserByIdAsync(id);
        if (user is null)
            return this.ToProblem(RondiTrack.Services.ServiceResultStatus.NotFound, "User not found.");

        try
        {
            user.Rename(request.Name);
            user.UpdateContactNumber(request.ContactNumber);
            return Ok(UserMapper.ToResponse(user));
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _store.DeleteUserAsync(id);
        // 204 on success stays exactly as-is — Problem Details only applies to errors.
        return deleted
            ? NoContent()
            : this.ToProblem(RondiTrack.Services.ServiceResultStatus.NotFound, "User not found.");
    }
}