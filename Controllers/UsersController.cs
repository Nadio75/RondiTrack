namespace RondiTrack.Controllers;

using Microsoft.AspNetCore.Mvc;
using RondiTrack.Data;
using RondiTrack.Models;
using RondiTrack.Mapping;
using RondiTrack.Exceptions;

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
        if (user is null) throw new NotFoundException("User not found.");
        return Ok(UserMapper.ToResponse(user));
    }

    [HttpPost]
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
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _store.DeleteUserAsync(id);
        if (!deleted) throw new NotFoundException("User not found.");
        return NoContent();
    }
}