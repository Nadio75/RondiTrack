using RondiTrack.Models.Dtos;

namespace RondiTrack.Services;

public sealed record RotationMember(
    Guid UserId,
    string Name,
    DateTime JoinedAtUtc);

public sealed record RotationPayment(
    Guid RecipientUserId,
    decimal Amount);

public static class PayoutRotationCalculator
{
    public static IReadOnlyList<PayoutRotationEntryResponse> Calculate(
        IEnumerable<RotationMember> members,
        IEnumerable<RotationPayment> payments,
        decimal contributionAmount)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(payments);

        if (contributionAmount <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(contributionAmount));

        var paidAmounts = payments
            .GroupBy(p => p.RecipientUserId)
            .ToDictionary(
                g => g.Key,
                g => g.First().Amount);

        var orderedMembers = members
            .OrderBy(m => m.JoinedAtUtc)
            .ThenBy(m => m.UserId)
            .ToList();

        var nextMemberId = orderedMembers
            .Where(m => !paidAmounts.ContainsKey(m.UserId))
            .Select(m => (Guid?)m.UserId)
            .FirstOrDefault();

        return orderedMembers
            .Select((member, index) =>
            {
                var isPaid = paidAmounts.TryGetValue(
                    member.UserId,
                    out var paidAmount);

                var isNext = !isPaid &&
                    nextMemberId == member.UserId;

                var amount = isPaid
                    ? paidAmount
                    : contributionAmount;

                var amountCents = checked(
                    (long)(amount * 100m));

                return new PayoutRotationEntryResponse(
                    member.UserId,
                    member.Name,
                    index + 1,
                    amountCents,
                    null,
                    isPaid ? "paid" : isNext ? "next" : "upcoming",
                    isNext);
            })
            .ToList();
    }
}
