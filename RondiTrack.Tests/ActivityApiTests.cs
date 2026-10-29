using System.Net;
using System.Net.Http.Json;
using RondiTrack.Models.Dtos;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

[Collection("Postgres collection")]
public sealed class ActivityApiTests
{
    private readonly PostgresApiFactory _factory;

    public ActivityApiTests(PostgresApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Member_CanReadOwnContributionActivity()
    {
        using var client = _factory.CreateClient();

        var scenario = await client.ArrangeMemberWithCycleAsync(_factory);

        using var created = await client.PostContributionAsync(
            scenario.Stokvel.Id,
            Guid.NewGuid().ToString(),
            ApiTestHelpers.ContributionBody(scenario));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var response = await client.GetAsync("/api/v1/activity");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<
            PagedResponse<ActivityFeedItem>>();

        Assert.NotNull(page);

        Assert.Contains(page.Items, item =>
            item.StokvelId == scenario.Stokvel.Id &&
            item.MemberId == scenario.User.Id &&
            item.ActivityType == "contribution" &&
            item.Amount == 500m);
    }

    [Fact]
    public async Task Member_CannotSeeOtherMembersContributionActivity()
    {
        using var client = _factory.CreateClient();

        var scenario = await client.ArrangeMemberWithCycleAsync(_factory);

        var secondMember = await client.CreateUserAsync(
            "Activity Second Member");

        using var membership = await client.AddMemberAsync(
            scenario.Stokvel.Id,
            secondMember.Id);

        Assert.True(membership.IsSuccessStatusCode);

        using var created = await client.PostContributionAsync(
            scenario.Stokvel.Id,
            Guid.NewGuid().ToString(),
            ApiTestHelpers.ContributionBody(scenario));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        client.UseTestUser(secondMember.Id);

        var response = await client.GetAsync("/api/v1/activity");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<
            PagedResponse<ActivityFeedItem>>();

        Assert.NotNull(page);

        Assert.DoesNotContain(page.Items, item =>
            item.MemberId == scenario.User.Id);
    }

    [Fact]
    public async Task InvalidPageSize_Returns400()
    {
        using var client = _factory.CreateClient();

        await client.ArrangeMemberWithCycleAsync(_factory);

        var response = await client.GetAsync(
            "/api/v1/activity?pageSize=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InvalidPageToken_Returns400()
    {
        using var client = _factory.CreateClient();

        await client.ArrangeMemberWithCycleAsync(_factory);

        var response = await client.GetAsync(
            "/api/v1/activity?pageToken=invalid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    [Fact]
    public async Task Pagination_ReturnsDistinctActivities()
    {
        using var client = _factory.CreateClient();

        var first = await client.ArrangeMemberWithCycleAsync(_factory);

        using var firstCreated = await client.PostContributionAsync(
            first.Stokvel.Id,
            Guid.NewGuid().ToString(),
            ApiTestHelpers.ContributionBody(first));

        Assert.Equal(HttpStatusCode.Created, firstCreated.StatusCode);

        // Create a second stokvel and add the first member to it.
        var secondStokvel = await client.CreateStokvelAsync();

        using var membership = await client.AddMemberAsync(
            secondStokvel.Id,
            first.User.Id);

        Assert.True(membership.IsSuccessStatusCode);

        // The second stokvel's creator is still authenticated here.
        // Create its cycle before switching to the other member.
        var secondCycle = await client.CreateCycleAsync(
            secondStokvel.Id,
            "2026-10");

        client.UseTestUser(first.User.Id);

        var secondBody = new
        {
            userId = first.User.Id,
            amount = 500m,
            contributionCycleId = secondCycle.Id
        };

        using var secondCreated = await client.PostContributionAsync(
            secondStokvel.Id,
            Guid.NewGuid().ToString(),
            secondBody);

        Assert.Equal(HttpStatusCode.Created, secondCreated.StatusCode);

        var firstResponse = await client.GetAsync(
            "/api/v1/activity?pageSize=1");

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var firstPage = await firstResponse.Content.ReadFromJsonAsync<
            PagedResponse<ActivityFeedItem>>();

        Assert.NotNull(firstPage);
        Assert.Single(firstPage.Items);
        Assert.False(string.IsNullOrWhiteSpace(firstPage.NextPageToken));

        var token = Uri.EscapeDataString(firstPage.NextPageToken);

        var secondResponse = await client.GetAsync(
            "/api/v1/activity?pageSize=1&pageToken=" + token);

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        var secondPage = await secondResponse.Content.ReadFromJsonAsync<
            PagedResponse<ActivityFeedItem>>();

        Assert.NotNull(secondPage);
        Assert.Single(secondPage.Items);

        Assert.NotEqual(
            firstPage.Items[0].Id,
            secondPage.Items[0].Id);

        Assert.Null(secondPage.NextPageToken);
    }
    [Fact]
    public async Task PageToken_CannotBeUsedByAnotherMember()
    {
        using var client = _factory.CreateClient();

        var first = await client.ArrangeMemberWithCycleAsync(_factory);

        var secondMember = await client.CreateUserAsync(
            "Activity Bookmark Second Member");

        using var membership = await client.AddMemberAsync(
            first.Stokvel.Id,
            secondMember.Id);

        Assert.True(membership.IsSuccessStatusCode);

        using var firstCreated = await client.PostContributionAsync(
            first.Stokvel.Id,
            Guid.NewGuid().ToString(),
            ApiTestHelpers.ContributionBody(first));

        Assert.Equal(HttpStatusCode.Created, firstCreated.StatusCode);

        var secondStokvel = await client.CreateStokvelAsync();

        using var secondMembership = await client.AddMemberAsync(
            secondStokvel.Id,
            first.User.Id);

        Assert.True(secondMembership.IsSuccessStatusCode);

        var secondCycle = await client.CreateCycleAsync(
            secondStokvel.Id,
            "2026-10");

        client.UseTestUser(first.User.Id);

        using var secondCreated = await client.PostContributionAsync(
            secondStokvel.Id,
            Guid.NewGuid().ToString(),
            new
            {
                userId = first.User.Id,
                amount = 500m,
                contributionCycleId = secondCycle.Id
            });

        Assert.Equal(HttpStatusCode.Created, secondCreated.StatusCode);

        var firstResponse = await client.GetAsync(
            "/api/v1/activity?pageSize=1");

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var firstPage = await firstResponse.Content.ReadFromJsonAsync<
            PagedResponse<ActivityFeedItem>>();

        Assert.NotNull(firstPage);
        Assert.False(string.IsNullOrWhiteSpace(firstPage.NextPageToken));

        // Another member tries to reuse the first member's bookmark.
        client.UseTestUser(secondMember.Id);

        var token = Uri.EscapeDataString(firstPage.NextPageToken);

        var response = await client.GetAsync(
            "/api/v1/activity?pageSize=1&pageToken=" + token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    [Fact]
    public async Task ProcessedPayout_AppearsInRecipientActivity()
    {
        using var client = _factory.CreateClient();

        var scenario = await client.ArrangeMemberWithCycleAsync(_factory);

        var payoutResponse = await client.PostAsync(
            $"/api/v1/stokvels/{scenario.Stokvel.Id}/cycles/{scenario.Cycle.Id}/payout",
            null);

        Assert.Equal(HttpStatusCode.Created, payoutResponse.StatusCode);

        var payout = await payoutResponse.Content.ReadFromJsonAsync<
            PayoutResponse>();

        Assert.NotNull(payout);

        // The activity feed belongs to the authenticated member.
        client.UseTestUser(payout.RecipientUserId);

        var response = await client.GetAsync("/api/v1/activity");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<
            PagedResponse<ActivityFeedItem>>();

        Assert.NotNull(page);

        Assert.Contains(page.Items, item =>
            item.ActivityType == "payout" &&
            item.StokvelId == scenario.Stokvel.Id &&
            item.MemberId == payout.RecipientUserId &&
            item.Amount == payout.Amount);
    }
}
