
namespace RondiTrack.Models.Dtos;

public record ContributionListItem(
    Guid Id,
    decimal Amount,
    string MemberName,
    DateTime CreatedAt
)
{
    public Guid ContributionCycleId { get; init; }
}
