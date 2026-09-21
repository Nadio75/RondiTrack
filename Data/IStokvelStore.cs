namespace RondiTrack.Data;

using RondiTrack.Models;

// An interface = a promise of what methods exist, with no implementation.
// Our controllers will only ever talk to THIS, never directly to the
// in-memory list below. If we swap this for a real database later, controllers won't need to change at all.
public interface IStokvelStore
{
    // Task<T> everywhere because the assignment requires async all the way
    // down, even though right now we're just reading from memory (no real
    // I/O happening yet — but the shape must already be async).
    Task<IEnumerable<User>> GetAllUsersAsync();
    Task<User?> GetUserByIdAsync(Guid id);
    Task AddUserAsync(User user);
    Task<bool> DeleteUserAsync(Guid id);

    Task<IEnumerable<Stokvel>> GetAllStokvelsAsync();
    Task<Stokvel?> GetStokvelByIdAsync(Guid id);
    Task AddStokvelAsync(Stokvel stokvel);
    Task<bool> DeleteStokvelAsync(Guid id);
}