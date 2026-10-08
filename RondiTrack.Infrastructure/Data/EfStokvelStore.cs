namespace RondiTrack.Infrastructure;

using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;

public class EfStokvelStore : IStokvelStore
{
    private readonly RondiTrackDbContext _db;

    public EfStokvelStore(RondiTrackDbContext db) => _db = db;

    public async Task<IEnumerable<User>> GetAllUsersAsync() =>
        await _db.Users.AsNoTracking().ToListAsync();

    public async Task<User?> GetUserByIdAsync(Guid id) =>
        await _db.Users.FindAsync(id);

    public async Task AddUserAsync(User user)
    {
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
    }

    public async Task<bool> DeleteUserAsync(Guid id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return false;
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<Stokvel>> GetAllStokvelsAsync()
    {
        var stokvels = await _db.Stokvels.ToListAsync();
        await HydrateMembersAsync(stokvels);
        return stokvels;
    }

    public async Task<Stokvel?> GetStokvelByIdAsync(Guid id)
    {
        var stokvel = await _db.Stokvels.FindAsync(id);
        if (stokvel is null) return null;
        await HydrateMembersAsync(new[] { stokvel });
        return stokvel;
    }

    public async Task AddStokvelAsync(Stokvel stokvel)
    {
        _db.Stokvels.Add(stokvel);
        await _db.SaveChangesAsync();
    }

    public async Task<bool> DeleteStokvelAsync(Guid id)
    {
        var stokvel = await _db.Stokvels.FindAsync(id);
        if (stokvel is null) return false;
        _db.Stokvels.Remove(stokvel);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task AddMembershipAsync(Guid stokvelId, Guid userId)
    {
        _db.StokvelMembers.Add(new StokvelMember(stokvelId, userId));
        await _db.SaveChangesAsync();
    }

    // Rebuilds each Stokvel's private MemberIds list from the real
    // relational table — the translation the MemberIds mapping decision needs.
        private async Task HydrateMembersAsync(IReadOnlyCollection<Stokvel> stokvels)
    {
        var ids = stokvels.Select(s => s.Id).ToList();
        var members = await _db.StokvelMembers
            .Where(m => ids.Contains(m.StokvelId))
            .ToListAsync();

        foreach (var stokvel in stokvels)
            stokvel.LoadMembers(members.Where(m => m.StokvelId == stokvel.Id).Select(m => m.UserId));
    }
        public async Task AddMembershipAsync(Guid stokvelId, Guid userId, string role = "Member")
    {
        _db.StokvelMembers.Add(new StokvelMember(stokvelId, userId, role));
        await _db.SaveChangesAsync();
    }

    public async Task<StokvelMember?> GetMembershipAsync(Guid stokvelId, Guid userId) =>
        await _db.StokvelMembers.FindAsync(userId, stokvelId); // composite key: order matches HasKey

            public async Task<User?> GetUserByIdReadOnlyAsync(Guid id) =>
        await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);

    public async Task<Stokvel?> GetStokvelByIdReadOnlyAsync(Guid id)
    {
        var stokvel = await _db.Stokvels.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (stokvel is null) return null;
        await HydrateMembersAsync(new[] { stokvel });
        return stokvel;
    }
}
