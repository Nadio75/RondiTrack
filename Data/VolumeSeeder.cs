namespace RondiTrack.Data;

using Microsoft.EntityFrameworkCore;
using RondiTrack.Models;

public static class VolumeSeeder
{
    public static async Task SeedAsync(
        RondiTrackDbContext db,
        int contributionCount = 12000)
    {
        // 1. Find or create the volume-test stokvel.
        var stokvel = await db.Stokvels.FirstOrDefaultAsync();

        if (stokvel is null)
        {
            stokvel = new Stokvel("Volume Test Stokvel", 500m);
            db.Stokvels.Add(stokvel);
            await db.SaveChangesAsync();
        }

        // 2. Find or create the volume-test cycle.
        var cycle = await db.ContributionCycles
            .FirstOrDefaultAsync(c => c.StokvelId == stokvel.Id);

        if (cycle is null)
        {
            cycle = new ContributionCycle(
                stokvel.Id,
                "2026-10",
                500m * 50);

            db.ContributionCycles.Add(cycle);
            await db.SaveChangesAsync();
        }

        // 3. Find how many members this stokvel already has.
        var existingMemberCount = await db.StokvelMembers
            .CountAsync(m => m.StokvelId == stokvel.Id);

        Console.WriteLine(
            $"Existing members in volume stokvel: {existingMemberCount}");

        // 4. We need at least one unique member per contribution
        // because Contributions has a composite unique constraint:
        //
        // UserId + StokvelId + ContributionCycleId
        //
        // Therefore create enough members first.
        if (existingMemberCount < contributionCount)
        {
            var membersToCreate = contributionCount - existingMemberCount;

            Console.WriteLine(
                $"Creating {membersToCreate} additional members...");

            var membersBatch = new List<StokvelMember>(1000);

            for (int i = existingMemberCount; i < contributionCount; i++)
            {
                var user = new User(
                    $"Volume Member {i + 1}",
                    $"VOLUME-{i + 1:D6}");

                db.Users.Add(user);

                membersBatch.Add(
                    new StokvelMember(stokvel.Id, user.Id));

                if (membersBatch.Count == 1000)
                {
                    db.StokvelMembers.AddRange(membersBatch);
                    await db.SaveChangesAsync();
                    membersBatch.Clear();

                    Console.WriteLine(
                        $"  Members created: {i + 1}/{contributionCount}");
                }
            }

            if (membersBatch.Count > 0)
            {
                db.StokvelMembers.AddRange(membersBatch);
                await db.SaveChangesAsync();
            }
        }

        // 5. Get the required members for this stokvel.
        var memberIds = await db.StokvelMembers
            .Where(m => m.StokvelId == stokvel.Id)
            .OrderBy(m => m.UserId)
            .Select(m => m.UserId)
            .Take(contributionCount)
            .ToListAsync();

        Console.WriteLine(
            $"Members available for volume contributions: {memberIds.Count}");

        // 6. Check how many contributions already exist.
        var currentCount = await db.Contributions
            .CountAsync(c => c.ContributionCycleId == cycle.Id);

        Console.WriteLine(
            $"Existing contributions in volume cycle: {currentCount}");

        if (currentCount >= contributionCount)
        {
            Console.WriteLine(
                $"Already have {currentCount} contributions - skipping seed.");

            Console.WriteLine($"StokvelId : {stokvel.Id}");
            Console.WriteLine($"CycleId   : {cycle.Id}");

            return;
        }

        // 7. Determine which members have already contributed.
        var existingContributorIds = await db.Contributions
            .Where(c => c.ContributionCycleId == cycle.Id)
            .Select(c => c.UserId)
            .ToHashSetAsync();

        var contributionsToCreate =
            memberIds
                .Where(userId => !existingContributorIds.Contains(userId))
                .Take(contributionCount - currentCount)
                .ToList();

        Console.WriteLine(
            $"Creating {contributionsToCreate.Count} contributions...");

        // 8. Create exactly one contribution per member.
        var contributionBatch = new List<Contribution>(1000);

        for (int i = 0; i < contributionsToCreate.Count; i++)
        {
            var userId = contributionsToCreate[i];

            var amount =
                100m + ((i % 20) + 1) * 50m;

            var contribution = new Contribution(
                stokvel.Id,
                userId,
                amount,
                cycle.Id);

            contributionBatch.Add(contribution);

            if (contributionBatch.Count == 1000)
            {
                db.Contributions.AddRange(contributionBatch);
                await db.SaveChangesAsync();
                contributionBatch.Clear();

                Console.WriteLine(
                    $"  Contributions created: {i + 1}/{contributionsToCreate.Count}");
            }
        }

        if (contributionBatch.Count > 0)
        {
            db.Contributions.AddRange(contributionBatch);
            await db.SaveChangesAsync();
        }

        // 9. Verify final count.
        var finalCount = await db.Contributions
            .CountAsync(c => c.ContributionCycleId == cycle.Id);

        Console.WriteLine();
        Console.WriteLine("Volume seed complete.");
        Console.WriteLine($"Contributions in cycle: {finalCount}");
        Console.WriteLine($"StokvelId              : {stokvel.Id}");
        Console.WriteLine($"CycleId                : {cycle.Id}");
    }
}