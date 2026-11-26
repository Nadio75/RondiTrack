using System.Net;
using System.Net.Http.Json;
using RondiTrack.Models.Dtos;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

[Collection("Postgres collection")]
public class MembershipRemovalApiTests
{
    private readonly PostgresApiFactory _factory;
    private readonly HttpClient _client;

    public MembershipRemovalApiTests(PostgresApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(Guid StokvelId, Guid ChairId, Guid MemberId)>
        CreateGroupAsync(bool addMember = true)
    {
        var chair = await _client.CreateUserAsync("Test Chairperson");
        var member = await _client.CreateUserAsync("Test Member");

        _client.UseTestUser(chair.Id);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/stokvels",
            new
            {
                name = "Membership Test " + Guid.NewGuid(),
                contributionAmount = 500
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var stokvel = await response.Content
            .ReadFromJsonAsync<StokvelResponse>();

        Assert.NotNull(stokvel);

        if (addMember)
        {
            await _factory.EnsureMembershipAsync(
                stokvel.Id, member.Id, "Member");
        }

        return (stokvel.Id, chair.Id, member.Id);
    }

    [Fact]
    public async Task Member_can_leave_unused_stokvel()
    {
        var group = await CreateGroupAsync();

        _client.UseTestUser(group.MemberId);

        var response = await _client.DeleteAsync(
            $"/api/v1/stokvels/{group.StokvelId}/members/me");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var read = await _client.GetAsync(
            $"/api/v1/stokvels/{group.StokvelId}");

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);

        _client.UseTestUser(group.ChairId);

        var remaining = await _client.GetFromJsonAsync<StokvelResponse>(
            $"/api/v1/stokvels/{group.StokvelId}");

        Assert.NotNull(remaining);
        Assert.Equal(1, remaining.MemberCount);
    }

    [Fact]
    public async Task Chairperson_cannot_leave_without_transfer()
    {
        var group = await CreateGroupAsync();

        var response = await _client.DeleteAsync(
            $"/api/v1/stokvels/{group.StokvelId}/members/me");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Member_cannot_delete_stokvel()
    {
        var group = await CreateGroupAsync();

        _client.UseTestUser(group.MemberId);

        var response = await _client.DeleteAsync(
            $"/api/v1/stokvels/{group.StokvelId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Chairperson_cannot_delete_group_with_other_members()
    {
        var group = await CreateGroupAsync();

        var response = await _client.DeleteAsync(
            $"/api/v1/stokvels/{group.StokvelId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var remaining = await _client.GetFromJsonAsync<StokvelResponse>(
            $"/api/v1/stokvels/{group.StokvelId}");

        Assert.NotNull(remaining);
        Assert.Equal(2, remaining.MemberCount);
    }

    [Fact]
    public async Task Member_cannot_leave_during_open_cycle()
    {
        var group = await CreateGroupAsync();

        await _client.CreateCycleAsync(
            group.StokvelId, "2027-12");

        _client.UseTestUser(group.MemberId);

        var response = await _client.DeleteAsync(
            $"/api/v1/stokvels/{group.StokvelId}/members/me");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Outsider_cannot_remove_membership()
    {
        var group = await CreateGroupAsync(false);

        _client.UseTestUser(group.MemberId);

        var response = await _client.DeleteAsync(
            $"/api/v1/stokvels/{group.StokvelId}/members/me");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Chairperson_cannot_delete_stokvel_with_contributions()
    {
        var group = await CreateGroupAsync(false);

        var cycle = await _client.CreateCycleAsync(
            group.StokvelId, "2027-12");

        var recorded = await _client.PostContributionAsync(
            group.StokvelId,
            Guid.NewGuid().ToString(),
            new
            {
                userId = group.ChairId,
                amount = 500,
                contributionCycleId = cycle.Id
            });

        Assert.Equal(HttpStatusCode.Created, recorded.StatusCode);

        var deleted = await _client.DeleteAsync(
            $"/api/v1/stokvels/{group.StokvelId}");

        Assert.Equal(HttpStatusCode.Conflict, deleted.StatusCode);

        var remaining = await _client.GetAsync(
            $"/api/v1/stokvels/{group.StokvelId}");

        Assert.Equal(HttpStatusCode.OK, remaining.StatusCode);

        using var scope =
            Microsoft.Extensions.DependencyInjection
                .ServiceProviderServiceExtensions
                .CreateScope(_factory.Services);

        var db = Microsoft.Extensions.DependencyInjection
            .ServiceProviderServiceExtensions
            .GetRequiredService<
                RondiTrack.Infrastructure.RondiTrackDbContext>(
                    scope.ServiceProvider);

        Assert.True(db.Contributions.Any(c =>
            c.StokvelId == group.StokvelId &&
            c.UserId == group.ChairId &&
            c.ContributionCycleId == cycle.Id));
    }
}
