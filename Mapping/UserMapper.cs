namespace RondiTrack.Mapping;

using RondiTrack.Models;

public static class UserMapper
{
    // One clear, explicit conversion point from entity to response — no magic, no library.
    public static UserResponse ToResponse(User user) => UserResponse.FromUser(user);
}