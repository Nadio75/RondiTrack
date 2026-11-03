using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;
using RondiTrack.Infrastructure;

namespace RondiTrack.Services;

public sealed record CycleArrearPreview(
    Guid UserId,
    string MemberName,
    long PaidCents,
    long ArrearsCents);

public sealed record CycleClosingPreview(
    Guid StokvelId,
    Guid CycleId,
    string Period,
    DateTime ClosingAtUtc,
    uint Version,
    long TargetAmountCents,
    long TotalArrearsCents,
    IReadOnlyList<CycleArrearPreview> Arrears,string SnapshotToken);

public sealed class CycleClosingPreviewService
{
    private readonly RondiTrackDbContext _db;

    public CycleClosingPreviewService(RondiTrackDbContext db)
    {
        _db = db;
    }

    public async Task<CycleClosingPreview?> CalculateAsync(
        Guid stokvelId,
        Guid cycleId,
        CancellationToken cancellationToken = default)
    {
        var cycle = await _db.ContributionCycles
            .AsNoTracking()
            .SingleOrDefaultAsync(
                c => c.Id == cycleId &&
                     c.StokvelId == stokvelId,
                cancellationToken);

        if (cycle is null)
            return null;

        var stokvel = await _db.Stokvels
            .AsNoTracking()
            .SingleOrDefaultAsync(
                s => s.Id == stokvelId,
                cancellationToken);

        if (stokvel is null)
            return null;

        if (!DateTime.TryParseExact(
                cycle.Period,
                "yyyy-MM",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var period))
        {
            return null;
        }

        var closingAtUtc =
            CycleClosingSchedule.GetClosingInstantUtc(
                period.Year,
                period.Month,
                stokvel.ClosingDayOfMonth);

        var members = await _db.StokvelMembers
            .AsNoTracking()
            .Where(m =>
                m.StokvelId == stokvelId &&
                m.JoinedAtUtc < closingAtUtc)
            .Select(m => new
            {
                m.UserId,
                MemberName = m.User.Name
            })
            .ToListAsync(cancellationToken);

        var payments = await _db.Contributions
            .AsNoTracking()
            .Where(c =>
                c.StokvelId == stokvelId &&
                c.ContributionCycleId == cycleId)
            .GroupBy(c => c.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                TotalPaid = g.Sum(c => c.Amount)
            })
            .ToListAsync(cancellationToken);

        var paidByMember = payments.ToDictionary(
            p => p.UserId,
            p => checked((long)decimal.Round(
                p.TotalPaid * 100m,
                0,
                MidpointRounding.AwayFromZero)));

        long targetCents = checked((long)decimal.Round(
            cycle.TargetAmount * 100m,
            0,
            MidpointRounding.AwayFromZero));

        var arrears = new List<CycleArrearPreview>();

        foreach (var member in members)
        {
            paidByMember.TryGetValue(
                member.UserId,
                out var paidCents);

            long owedCents = Math.Max(
                0,
                targetCents - paidCents);

            if (owedCents == 0)
                continue;

            arrears.Add(new CycleArrearPreview(
                member.UserId,
                member.MemberName,
                paidCents,
                owedCents));
        }

        var ordered = arrears
            .OrderBy(a => a.MemberName)
            .ThenBy(a => a.UserId)
            .ToArray();

        long totalCents = checked(
            ordered.Sum(a => a.ArrearsCents));

        var snapshotData = new StringBuilder();

snapshotData.Append(stokvelId).Append('|')
    .Append(cycleId).Append('|')
    .Append(cycle.Period).Append('|')
    .Append(closingAtUtc.Ticks).Append('|')
    .Append(cycle.xmin).Append('|')
    .Append(targetCents);

foreach (var member in members.OrderBy(m => m.UserId))
{
    paidByMember.TryGetValue(member.UserId, out var paidCents);

    snapshotData.Append('|')
        .Append(member.UserId)
        .Append(':')
        .Append(Convert.ToHexString(
            Encoding.UTF8.GetBytes(member.MemberName)))
        .Append(':')
        .Append(paidCents);
}

var snapshotToken = Convert.ToHexString(
    SHA256.HashData(
        Encoding.UTF8.GetBytes(snapshotData.ToString())));

return new CycleClosingPreview(
    stokvelId,
    cycleId,
    cycle.Period,
    closingAtUtc,
    cycle.xmin,
    targetCents,
    totalCents,
    ordered,
    snapshotToken);
    }
}

