namespace RondiTrack.Models;

public record UserResponse(
    Guid Id,
    string Name,
    string ContactNumber)
{
    // Converts an internal User entity into the shape a caller is allowed to see.
    public static UserResponse FromUser(User user) =>
        new(user.Id, user.Name, user.ContactNumber);
}