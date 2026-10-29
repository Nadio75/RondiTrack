using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Infrastructure;
using RondiTrack.Domain;using System.Net;
using System.Net.Http.Json;
using RondiTrack.Models.Dtos;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

[Collection("Postgres collection")]
public sealed class ContributionHistoryApiTests
{
    private readonly PostgresApiFactory _factory;

    public ContributionHistoryApiTests(PostgresApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Member_CanReadContributionHistory()
    {
        using var client = _factory.CreateClient();

        var scenario = await client.ArrangeMemberWithCycleAsync(_factory);

        using var created = await client.PostContributionAsync(
            scenario.Stokvel.Id,
            Guid.NewGuid().ToString(),
            ApiTestHelpers.ContributionBody(scenario));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var response = await client.GetAsync(
            $"/api/v1/stokvels/{scenario.Stokvel.Id}/contributions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<
            PagedResponse<StokvelContributionHistoryItem>>();

        Assert.NotNull(page);
        Assert.Contains(page.Items, item =>
            item.MemberId == scenario.User.Id &&
            item.Amount == 500m);
    }

    [Fact]
    public async Task Outsider_CannotReadContributionHistory()
    {
        using var client = _factory.CreateClient();

        var scenario = await client.ArrangeMemberWithCycleAsync(_factory);

        var outsider = await client.CreateUserAsync("History Outsider");

        client.UseTestUser(outsider.Id);

        var response = await client.GetAsync(
            $"/api/v1/stokvels/{scenario.Stokvel.Id}/contributions");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task InvalidMember_Returns400()
    {
        using var client = _factory.CreateClient();

        var scenario = await client.ArrangeMemberWithCycleAsync(_factory);

        var response = await client.GetAsync(
            $"/api/v1/stokvels/{scenario.Stokvel.Id}/contributions?member=invalid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReversedDateRange_Returns400()
    {
        using var client = _factory.CreateClient();

        var scenario = await client.ArrangeMemberWithCycleAsync(_factory);

        var response = await client.GetAsync(
            $"/api/v1/stokvels/{scenario.Stokvel.Id}/contributions" +
            "?from=2026-11-01T00%3A00%3A00Z" +
            "&to=2026-10-01T00%3A00%3A00Z");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InvalidPageSize_Returns400()
    {
        using var client = _factory.CreateClient();

        var scenario = await client.ArrangeMemberWithCycleAsync(_factory);

        var response = await client.GetAsync(
            $"/api/v1/stokvels/{scenario.Stokvel.Id}/contributions?pageSize=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InvalidPageToken_Returns400()
    {
        using var client = _factory.CreateClient();

        var scenario = await client.ArrangeMemberWithCycleAsync(_factory);

        var response = await client.GetAsync(
            $"/api/v1/stokvels/{scenario.Stokvel.Id}/contributions?pageToken=invalid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PageToken_CannotBeReusedWithDifferentFilters()
    {
        using var client = _factory.CreateClient();

        var scenario = await client.ArrangeMemberWithCycleAsync(_factory);

        var hash = RondiTrack.Helpers.ContributionHistoryPageToken
            .CreateFilterHash(
                scenario.Stokvel.Id,
                null,
                null,
                null);

        var token = RondiTrack.Helpers.ContributionHistoryPageToken
            .Encode(
                DateTime.UtcNow,
                Guid.NewGuid(),
                hash);

        var encodedToken = Uri.EscapeDataString(token);

        var response = await client.GetAsync(
            $"/api/v1/stokvels/{scenario.Stokvel.Id}/contributions" +
            $"?member={scenario.User.Id}&pageToken={encodedToken}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    [Fact]
    public async Task Pagination_ReturnsDistinctPages()
    {
        using var client = _factory.CreateClient();

        var scenario = await client.ArrangeMemberWithCycleAsync(_factory);

        var secondMember = await client.CreateUserAsync("Second Payer");

        using var membership = await client.AddMemberAsync(
            scenario.Stokvel.Id,
            secondMember.Id);

        Assert.True(membership.IsSuccessStatusCode);

        foreach (var userId in new[] { scenario.User.Id, secondMember.Id })
        {
            using var created = await client.PostContributionAsync(
                scenario.Stokvel.Id,
                Guid.NewGuid().ToString(),
                new
                {
                    userId,
                    amount = 500m,
                    contributionCycleId = scenario.Cycle.Id
                });

            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        var url = $"/api/v1/stokvels/{scenario.Stokvel.Id}/contributions";

        var firstResponse = await client.GetAsync(url + "?pageSize=1");

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var first = await firstResponse.Content.ReadFromJsonAsync<
            PagedResponse<StokvelContributionHistoryItem>>();

        Assert.NotNull(first);
        Assert.Single(first.Items);
        Assert.False(string.IsNullOrWhiteSpace(first.NextPageToken));

        var token = Uri.EscapeDataString(first.NextPageToken!);

        var secondResponse = await client.GetAsync(
            url + "?pageSize=1&pageToken=" + token);

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        var second = await secondResponse.Content.ReadFromJsonAsync<
            PagedResponse<StokvelContributionHistoryItem>>();

        Assert.NotNull(second);
        Assert.Single(second.Items);
        Assert.NotEqual(first.Items[0].Id, second.Items[0].Id);
        Assert.Null(second.NextPageToken);
    }

    [Fact]
    public async Task OldestSort_ReturnsChronologicalPages()
    {
        using var client = _factory.CreateClient();

        var scenario = await client.ArrangeMemberWithCycleAsync(_factory);

        var dates = new[]
        {
            new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 15, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 29, 10, 0, 0, DateTimeKind.Utc)
        };

        var memberIds = new List<Guid> { scenario.User.Id };

        for (var i = 1; i < dates.Length; i++)
        {
            var member = await client.CreateUserAsync(
                "Sort Member " + Guid.NewGuid());

            using var membership = await client.AddMemberAsync(
                scenario.Stokvel.Id,
                member.Id);

            Assert.True(membership.IsSuccessStatusCode);

            memberIds.Add(member.Id);
        }

        var contributionIds = new List<Guid>();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<RondiTrackDbContext>();

            for (var i = 0; i < dates.Length; i++)
            {
                var contribution = new Contribution(
                    scenario.Stokvel.Id,
                    memberIds[i],
                    500m,
                    scenario.Cycle.Id);

                db.Contributions.Add(contribution);

                db.Entry(contribution)
                    .Property(c => c.CreatedAt)
                    .CurrentValue = dates[i];

                contributionIds.Add(contribution.Id);
            }

            await db.SaveChangesAsync();
        }

        var url =
            $"/api/v1/stokvels/{scenario.Stokvel.Id}/contributions";

        string? pageToken = null;

        for (var i = 0; i < dates.Length; i++)
        {
            var query = "?sort=oldest&pageSize=1";

            if (pageToken is not null)
            {
                query += "&pageToken=" +
                    Uri.EscapeDataString(pageToken);
            }

            var response = await client.GetAsync(url + query);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var page = await response.Content.ReadFromJsonAsync<
                PagedResponse<StokvelContributionHistoryItem>>();

            Assert.NotNull(page);
            Assert.Single(page.Items);

            Assert.Equal(contributionIds[i], page.Items[0].Id);
            Assert.Equal(dates[i], page.Items[0].CreatedAt);

            if (i < dates.Length - 1)
            {
                Assert.False(string.IsNullOrWhiteSpace(
                    page.NextPageToken));
            }
            else
            {
                Assert.Null(page.NextPageToken);
            }

            pageToken = page.NextPageToken;
        }
    }
    [Fact]
    public async Task OctoberUtcRange_ExcludesNovemberMidnightSast()
    {
        using var client = _factory.CreateClient();

        var scenario = await client.ArrangeMemberWithCycleAsync(_factory);

        var url = $"/api/v1/stokvels/{scenario.Stokvel.Id}/contributions";

        var response = await client.GetAsync(
            url +
            "?from=2026-09-30T22%3A00%3A00Z" +
            "&to=2026-10-31T22%3A00%3A00Z");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<
            PagedResponse<StokvelContributionHistoryItem>>();

        Assert.NotNull(page);

        Assert.All(page.Items, item =>
        {
            Assert.True(item.CreatedAt >=
                new DateTime(2026, 9, 30, 22, 0, 0, DateTimeKind.Utc));

            Assert.True(item.CreatedAt <
                new DateTime(2026, 10, 31, 22, 0, 0, DateTimeKind.Utc));
        });
    }
    [Fact]
    public async Task OctoberBoundary_IncludesLastOctoberContribution()
    {
        using var client = _factory.CreateClient();

        var scenario = await client.ArrangeMemberWithCycleAsync(_factory);

        var octoberStart =
            new DateTime(2026, 9, 30, 22, 0, 0, DateTimeKind.Utc);

        var octoberLastNight =
            new DateTime(2026, 10, 31, 21, 30, 0, DateTimeKind.Utc);

        var novemberStart =
            new DateTime(2026, 10, 31, 22, 0, 0, DateTimeKind.Utc);

        var dates = new[]
        {
            octoberStart,
            octoberLastNight,
            novemberStart
        };

        var memberIds = new List<Guid> { scenario.User.Id };

        for (var i = 0; i < 2; i++)
        {
            var member = await client.CreateUserAsync(
                "Boundary Member " + Guid.NewGuid());

            using var membership = await client.AddMemberAsync(
                scenario.Stokvel.Id,
                member.Id);

            Assert.True(membership.IsSuccessStatusCode);

            memberIds.Add(member.Id);
        }

        var contributionIds = new List<Guid>();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<RondiTrackDbContext>();

            for (var i = 0; i < dates.Length; i++)
            {
                var contribution = new Contribution(
                    scenario.Stokvel.Id,
                    memberIds[i],
                    500m,
                    scenario.Cycle.Id);

                db.Contributions.Add(contribution);

                db.Entry(contribution)
                    .Property(c => c.CreatedAt)
                    .CurrentValue = dates[i];

                contributionIds.Add(contribution.Id);
            }

            await db.SaveChangesAsync();
        }

        var url = $"/api/v1/stokvels/{scenario.Stokvel.Id}/contributions";

        var response = await client.GetAsync(
            url +
            "?from=2026-09-30T22%3A00%3A00Z" +
            "&to=2026-10-31T22%3A00%3A00Z");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<
            PagedResponse<StokvelContributionHistoryItem>>();

        Assert.NotNull(page);

        Assert.Contains(page.Items, x => x.Id == contributionIds[0]);
        Assert.Contains(page.Items, x => x.Id == contributionIds[1]);
        Assert.DoesNotContain(page.Items, x => x.Id == contributionIds[2]);
    }
    [Fact]
    public async Task TamperedPageToken_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();

        var scenario = await client.ArrangeMemberWithCycleAsync(_factory);

        using var created = await client.PostContributionAsync(
            scenario.Stokvel.Id,
            Guid.NewGuid().ToString(),
            ApiTestHelpers.ContributionBody(scenario));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // An attacker supplies a bookmark that has not been
        // authenticated by the server.
        var forgedToken = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes(
                "{\"Version\":1,\"Id\":\"" +
                Guid.NewGuid() +
                "\"}"));

        var response = await client.GetAsync(
            $"/api/v1/stokvels/{scenario.Stokvel.Id}/contributions" +
            $"?pageToken={Uri.EscapeDataString(forgedToken)}");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }
}
