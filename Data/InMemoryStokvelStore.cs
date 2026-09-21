namespace RondiTrack.Data;

using RondiTrack.Models;
//This is where im gonna be keeping my dummy data.
public class InMemoryStokvelStore : IStokvelStore
{
    // Our "tables." Private, so nothing outside this class can reach in
    // and mutate them directly — all access goes through the methods below.
    private readonly List<User> _users = new();
    private readonly List<Stokvel> _stokvels = new();

    // Constructor runs once, when the singleton is first created.
    // We seed some starting data here so endpoints have something to return
    // immediately, per the assignment's requirement.
    public InMemoryStokvelStore()
    {
        var thabo = new User("Thabo Nkosi", "0821234567");
        var lindiwe = new User("Lindiwe Dlamini", "0837654321");
        var sipho = new User("Sipho Zulu", "0715551234");

        _users.Add(thabo);
        _users.Add(lindiwe);
        _users.Add(sipho);

        var savingsClub = new Stokvel("Soweto Savings Club", 500m);
        // 'm' suffix means "this literal is a decimal", not a double —
        // required in C# whenever you write a decimal value directly.
        savingsClub.AddMember(thabo.Id);
        savingsClub.AddMember(lindiwe.Id);

        _stokvels.Add(savingsClub);
    }

    public Task<IEnumerable<User>> GetAllUsersAsync()
    {
        // Task.FromResult wraps an already-available value in a "completed task"
        // — this is how you satisfy an async signature when there's no actual
        // waiting happening (no database call, no network call).
        return Task.FromResult<IEnumerable<User>>(_users);
    }

    public Task<User?> GetUserByIdAsync(Guid id)
    {
        // FirstOrDefault returns the matching user, or null if none found —
        // the '?' on User? in the interface signature says "this can be null."
        var user = _users.FirstOrDefault(u => u.Id == id);
        return Task.FromResult(user);
    }

    public Task AddUserAsync(User user)
    {
        _users.Add(user);
        return Task.CompletedTask; // signals "done" with no return value
    }

    public Task<bool> DeleteUserAsync(Guid id)
    {
        var user = _users.FirstOrDefault(u => u.Id == id);
        if (user is null)
        {
            return Task.FromResult(false); // nothing to delete — tell caller it failed
        }

        _users.Remove(user);
        return Task.FromResult(true);
    }

    public Task<IEnumerable<Stokvel>> GetAllStokvelsAsync()
    {
        return Task.FromResult<IEnumerable<Stokvel>>(_stokvels);
    }

    public Task<Stokvel?> GetStokvelByIdAsync(Guid id)
    {
        var stokvel = _stokvels.FirstOrDefault(s => s.Id == id);
        return Task.FromResult(stokvel);
    }

    public Task AddStokvelAsync(Stokvel stokvel)
    {
        _stokvels.Add(stokvel);
        return Task.CompletedTask;
    }

    public Task<bool> DeleteStokvelAsync(Guid id)
    {
        var stokvel = _stokvels.FirstOrDefault(s => s.Id == id);
        if (stokvel is null)
        {
            return Task.FromResult(false);
        }

        _stokvels.Remove(stokvel);
        return Task.FromResult(true);
    }
}