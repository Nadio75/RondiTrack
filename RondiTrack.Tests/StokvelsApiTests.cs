using RondiTrack.Models.Dtos;
using RondiTrack.Infrastructure;
using RondiTrack.Domain;
using RondiTrack.Tests.TestSupport;
namespace RondiTrack.Tests;

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RondiTrack.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Infrastructure;

[Collection("Postgres collection")]
public class StokvelsApiTests
{
    private readonly HttpClient _client;
private readonly PostgresApiFactory _factory;

public StokvelsApiTests(PostgresApiFactory factory)
{
    _factory = factory;
    _client = factory.CreateClient();
}
    // ---------- Stokvel happy paths ----------

    [Fact]
public async Task Creating_a_stokvel_returns_201_with_creator_as_chairperson()
{
    var creator = await _client.CreateUserAsync("Creator");

    _client.UseTestUser(creator.Id);

    var response = await _client.PostAsJsonAsync(
        "/api/v1/stokvels",
        new
        {
            name = "Happy Path Stokvel " + Guid.NewGuid(),
            contributionAmount = 500
        });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    var created = await response.Content
        .ReadFromJsonAsync<StokvelResponse>();

    Assert.NotNull(created);
    Assert.Equal(1, created.MemberCount);

    Assert.EndsWith(
        $"/api/v1/stokvels/{created.Id}",
        response.Headers.Location!.ToString());

    // The creator should immediately be allowed to read the stokvel.
    var fetched = await _client.GetFromJsonAsync<StokvelResponse>(
        $"/api/v1/stokvels/{created.Id}");

    Assert.NotNull(fetched);
    Assert.Equal(created.Id, fetched.Id);
    Assert.Equal(1, fetched.MemberCount);

    // Verify that the database stored the correct role.
    using var scope = _factory.Services.CreateScope();

    var db = scope.ServiceProvider
        .GetRequiredService<RondiTrackDbContext>();

    var membership = await db.StokvelMembers
        .SingleAsync(m =>
            m.StokvelId == created.Id &&
            m.UserId == creator.Id);

    Assert.Equal("Chairperson", membership.Role);
}

    [Fact]
    public async Task Listing_stokvels_includes_a_stokvel_that_was_just_created()
    {
        var stokvel = await _client.CreateStokvelAsync();

        var all = await _client.GetFromJsonAsync<List<StokvelResponse>>("/api/v1/stokvels");

        Assert.Contains(all!, s => s.Id == stokvel.Id);
    }

    [Fact]
   
public async Task Updating_a_stokvel_replaces_name_and_contribution_amount()
{
    var stokvel = await _client.CreateStokvelAsync();

    await _client.ArrangeChairpersonAsync(_factory, stokvel.Id);

    var response = await _client.PutAsJsonAsync(
        $"/api/v1/stokvels/{stokvel.Id}",
        new { name = "Renamed Stokvel", contributionAmount = 750 });

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var updated = await response.Content.ReadFromJsonAsync<StokvelResponse>();

    Assert.Equal(stokvel.Id, updated!.Id);
    Assert.Equal("Renamed Stokvel", updated.Name);
    Assert.Equal(750m, updated.ContributionAmount);
}

   [Fact]
public async Task Deleting_a_stokvel_returns_204_then_404_on_a_second_delete()
{
    var creator = await _client.CreateUserAsync("Deletion Test Creator");

        _client.UseTestUser(creator.Id);

        var created = await _client.PostAsJsonAsync(
            "/api/v1/stokvels",
            new
            {
                name = "Deletion Test " + Guid.NewGuid(),
                contributionAmount = 500
            });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var stokvel = await created.Content
            .ReadFromJsonAsync<StokvelResponse>();

        Assert.NotNull(stokvel);
        Assert.Equal(1, stokvel.MemberCount);

    var first = await _client.DeleteAsync($"/api/v1/stokvels/{stokvel.Id}");
    var second = await _client.DeleteAsync($"/api/v1/stokvels/{stokvel.Id}");

    Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
    second.AssertProblem(HttpStatusCode.NotFound);
}

    // ---------- Stokvel validation (400) and not found (404) ----------

    [Theory]
    [InlineData("", 500)]
    [InlineData("Valid Name", 0)]
    [InlineData("Valid Name", -5)]
    public async Task Creating_a_stokvel_that_breaks_a_rule_returns_400(string name, int contributionAmount)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/stokvels", new { name, contributionAmount });

        response.AssertProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Fetching_a_stokvel_with_an_id_that_is_not_a_guid_returns_400()
    {
        var response = await _client.GetAsync("/api/v1/stokvels/not-a-guid");

        response.AssertProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Updating_with_an_invalid_body_returns_400_even_when_the_stokvel_does_not_exist()
    {
        var response = await _client.PutAsJsonAsync($"/api/v1/stokvels/{Guid.NewGuid()}",
            new { name = "", contributionAmount = -1 });

        response.AssertProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Updating_a_stokvel_that_does_not_exist_returns_404()
    {
        var response = await _client.PutAsJsonAsync($"/api/v1/stokvels/{Guid.NewGuid()}",
            new { name = "Valid Name", contributionAmount = 500 });

        response.AssertProblem(HttpStatusCode.NotFound);
    }

    // ---------- Membership ----------

    [Fact]
public async Task Adding_a_member_returns_204_and_raises_the_member_count_to_two()
{
    var stokvel = await _client.CreateStokvelAsync();


    var user = await _client.CreateUserAsync();

    var response = await _client.AddMemberAsync(stokvel.Id, user.Id);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

    var after = await _client.GetFromJsonAsync<StokvelResponse>(
        $"/api/v1/stokvels/{stokvel.Id}");

    Assert.Equal(2, after!.MemberCount);
}
    [Fact]
public async Task Adding_the_same_member_twice_returns_409_and_the_count_stays_at_two()
{
    var stokvel = await _client.CreateStokvelAsync();


    var user = await _client.CreateUserAsync();

    var first = await _client.AddMemberAsync(stokvel.Id, user.Id);
    Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

    var second = await _client.AddMemberAsync(stokvel.Id, user.Id);
    second.AssertProblem(HttpStatusCode.Conflict);

    var after = await _client.GetFromJsonAsync<StokvelResponse>(
        $"/api/v1/stokvels/{stokvel.Id}");

    Assert.Equal(2, after!.MemberCount);
}
    [Fact]
    public async Task Adding_a_user_that_does_not_exist_returns_404()
    {
        var stokvel = await _client.CreateStokvelAsync();

        var response = await _client.AddMemberAsync(stokvel.Id, Guid.NewGuid());

        response.AssertProblem(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Adding_a_member_to_a_stokvel_that_does_not_exist_returns_404()
    {
        var user = await _client.CreateUserAsync();

        var response = await _client.AddMemberAsync(Guid.NewGuid(), user.Id);

        response.AssertProblem(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Adding_a_member_with_an_empty_guid_returns_400()
    {
        var stokvel = await _client.CreateStokvelAsync();

        var response = await _client.AddMemberAsync(stokvel.Id, Guid.Empty);

        response.AssertProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Creating_stokvel_with_first_cycle_saves_schedule()
    {
        var creator = await _client.CreateUserAsync(
            "Schedule Test Creator");

        _client.UseTestUser(creator.Id);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/stokvels",
            new
            {
                name = "Schedule Test " + Guid.NewGuid(),
                contributionAmount = 1500,
                rules = "Pay on time.",
                closingDayOfMonth = 20,
                firstCyclePeriod = "2027-12"
            });

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var created = await response.Content
            .ReadFromJsonAsync<StokvelResponse>();

        Assert.NotNull(created);
        Assert.Equal(1, created.MemberCount);
        Assert.Equal(20, created.ClosingDayOfMonth);
        Assert.Equal("Pay on time.", created.Rules);

        using var scope = _factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<RondiTrackDbContext>();

        var group = await db.Stokvels
            .SingleAsync(s => s.Id == created.Id);

        Assert.Equal(20, group.ClosingDayOfMonth);
        Assert.Equal("Pay on time.", group.Rules);

        var cycle = await db.ContributionCycles
            .SingleAsync(c => c.StokvelId == created.Id);

        Assert.Equal("2027-12", cycle.Period);
        Assert.Equal(1500m, cycle.TargetAmount);
        Assert.False(cycle.IsClosed);

        var membership = await db.StokvelMembers
            .SingleAsync(m => m.StokvelId == created.Id);

        Assert.Equal(creator.Id, membership.UserId);
        Assert.Equal("Chairperson", membership.Role);

        var fetched = await _client
            .GetFromJsonAsync<StokvelResponse>(
                $"/api/v1/stokvels/{created.Id}");

        Assert.NotNull(fetched);
        Assert.Equal(20, fetched.ClosingDayOfMonth);
        Assert.Equal("Pay on time.", fetched.Rules);
    }

    [Theory]
    [InlineData(0, "2027-12")]
    [InlineData(32, "2027-12")]
    [InlineData(25, "2027-13")]
    [InlineData(25, "December")]
    public async Task Invalid_schedule_does_not_create_stokvel(
        int closingDay,
        string period)
    {
        var creator = await _client.CreateUserAsync(
            "Invalid Schedule Creator");

        _client.UseTestUser(creator.Id);

        var name = "Invalid Schedule " + Guid.NewGuid();

        var response = await _client.PostAsJsonAsync(
            "/api/v1/stokvels",
            new
            {
                name,
                contributionAmount = 500,
                closingDayOfMonth = closingDay,
                firstCyclePeriod = period
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        using var scope = _factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<RondiTrackDbContext>();

        Assert.False(await db.Stokvels
            .AnyAsync(s => s.Name == name));
    }

    [Fact]
    public async Task Creating_without_schedule_preserves_defaults()
    {
        var created = await _client.CreateStokvelAsync();

        using var scope = _factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<RondiTrackDbContext>();

        var group = await db.Stokvels
            .SingleAsync(s => s.Id == created.Id);

        Assert.Equal(25, group.ClosingDayOfMonth);

        Assert.False(await db.ContributionCycles
            .AnyAsync(c => c.StokvelId == created.Id));
    }
}
