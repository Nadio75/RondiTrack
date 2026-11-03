using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

[Collection("Postgres collection")]
public sealed class InvitationApiTests
{
    private readonly PostgresApiFactory _factory;

    public InvitationApiTests(PostgresApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Chairperson_CanCreateInvitation()
    {
        using var client = _factory.CreateClient();

        var stokvel = await client.CreateStokvelAsync();

        var response = await client.PostAsync(
            $"/api/v1/stokvels/{stokvel.Id}/invites",
            null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var invitation = await response.Content
            .ReadFromJsonAsync<InvitationResponse>();

        Assert.NotNull(invitation);
        Assert.False(string.IsNullOrWhiteSpace(invitation.Code));
        Assert.True(invitation.ExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task OrdinaryMember_CannotCreateInvitation()
    {
        using var client = _factory.CreateClient();

        var stokvel = await client.CreateStokvelAsync();

        var member = await client.CreateUserAsync(
            "Invitation Ordinary Member");

        using var added = await client.AddMemberAsync(
            stokvel.Id,
            member.Id);

        Assert.True(added.IsSuccessStatusCode);

        client.UseTestUser(member.Id);

        var response = await client.PostAsync(
            $"/api/v1/stokvels/{stokvel.Id}/invites",
            null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnknownInvitation_Returns404()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(
            "/api/v1/invites/unknown-invitation-code");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ValidInvitation_PreviewContainsOnlySafeFields()
    {
        using var client = _factory.CreateClient();
        var stokvel = await client.CreateStokvelAsync();

        using var created = await client.PostAsync(
            $"/api/v1/stokvels/{stokvel.Id}/invites", null);

        created.EnsureSuccessStatusCode();

        var invitation = (await created.Content
            .ReadFromJsonAsync<InvitationResponse>())!;

        var newcomer = await client.CreateUserAsync("Preview Newcomer");
        client.UseTestUser(newcomer.Id);

        using var response = await client.GetAsync(
            $"/api/v1/invites/{invitation.Code}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = await System.Text.Json.JsonDocument
            .ParseAsync(await response.Content.ReadAsStreamAsync());

        var root = json.RootElement;

        Assert.Equal(stokvel.Id,
            root.GetProperty("stokvelId").GetGuid());

        var expected = new[]
        {
            "stokvelId",
            "name",
            "rules",
            "contributionAmount",
            "closingDayOfMonth",
            "memberCount"
        };

        Assert.Equal(
            expected.OrderBy(x => x),
            root.EnumerateObject()
                .Select(p => p.Name)
                .OrderBy(x => x));
    }

    [Fact]
    public async Task NewUser_CanJoinWithValidInvitation()
    {
        using var client = _factory.CreateClient();
        var stokvel = await client.CreateStokvelAsync();

        using var created = await client.PostAsync(
            $"/api/v1/stokvels/{stokvel.Id}/invites", null);

        created.EnsureSuccessStatusCode();

        var invitation = (await created.Content
            .ReadFromJsonAsync<InvitationResponse>())!;

        var newcomer = await client.CreateUserAsync("Joining Newcomer");
        client.UseTestUser(newcomer.Id);

        using var response = await client.PostAsync(
            $"/api/v1/invites/{invitation.Code}/join", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<
            RondiTrack.Infrastructure.RondiTrackDbContext>();

        var membership = await db.StokvelMembers
            .SingleAsync(m =>
                m.StokvelId == stokvel.Id &&
                m.UserId == newcomer.Id);

        Assert.Equal("Member", membership.Role);
    }

    [Fact]
    public async Task JoiningTwice_Returns409WithoutDuplicate()
    {
        using var client = _factory.CreateClient();
        var stokvel = await client.CreateStokvelAsync();

        using var created = await client.PostAsync(
            $"/api/v1/stokvels/{stokvel.Id}/invites", null);

        created.EnsureSuccessStatusCode();

        var invitation = (await created.Content
            .ReadFromJsonAsync<InvitationResponse>())!;

        var newcomer = await client.CreateUserAsync("Repeat Join Newcomer");
        client.UseTestUser(newcomer.Id);

        using var first = await client.PostAsync(
            $"/api/v1/invites/{invitation.Code}/join", null);

        using var second = await client.PostAsync(
            $"/api/v1/invites/{invitation.Code}/join", null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<
            RondiTrack.Infrastructure.RondiTrackDbContext>();

        var count = await db.StokvelMembers.CountAsync(m =>
            m.StokvelId == stokvel.Id &&
            m.UserId == newcomer.Id);

        Assert.Equal(1, count);
    }
    [Fact]
    public async Task ExpiredInvitation_CannotBePreviewedOrJoined()
    {
        using var client = _factory.CreateClient();
        var stokvel = await client.CreateStokvelAsync();

        using var created = await client.PostAsync(
            $"/api/v1/stokvels/{stokvel.Id}/invites", null);

        created.EnsureSuccessStatusCode();

        var invitation = (await created.Content
            .ReadFromJsonAsync<InvitationResponse>())!;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<
                RondiTrack.Infrastructure.RondiTrackDbContext>();

            var codeHash =
                RondiTrack.Helpers.InvitationCodeHelper.HashCode(
                    invitation.Code);

            await db.StokvelInvitations
                .Where(i => i.CodeHash == codeHash)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(
                        i => i.ExpiresAtUtc,
                        DateTime.UtcNow.AddMinutes(-1)));
        }

        var newcomer = await client.CreateUserAsync(
            "Expired Invitation Newcomer");

        client.UseTestUser(newcomer.Id);

        using var preview = await client.GetAsync(
            $"/api/v1/invites/{invitation.Code}");

        using var join = await client.PostAsync(
            $"/api/v1/invites/{invitation.Code}/join", null);

        Assert.Equal(HttpStatusCode.NotFound, preview.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, join.StatusCode);
    }
    [Fact]
    public async Task InvitationPreview_ShowsSavedStokvelRules()
    {
        using var client = _factory.CreateClient();

        var creator = await client.CreateUserAsync("Rules Test Creator");
        client.UseTestUser(creator.Id);

        const string expectedRules =
            "Members must contribute R500 before the 25th.";

        using var created = await client.PostAsJsonAsync(
            "/api/v1/stokvels",
            new
            {
                name = "Rules Test Stokvel",
                contributionAmount = 500m,
                rules = expectedRules
            });

        created.EnsureSuccessStatusCode();

        using var stokvelJson = JsonDocument.Parse(
            await created.Content.ReadAsStringAsync());

        var stokvelId = stokvelJson.RootElement
            .GetProperty("id")
            .GetGuid();

        using var invitationResponse = await client.PostAsync(
            $"/api/v1/stokvels/{stokvelId}/invites",
            null);

        invitationResponse.EnsureSuccessStatusCode();

        var invitation = (await invitationResponse.Content
            .ReadFromJsonAsync<InvitationResponse>())!;

        using var preview = await client.GetAsync(
            $"/api/v1/invites/{invitation.Code}");

        preview.EnsureSuccessStatusCode();

        using var previewJson = JsonDocument.Parse(
            await preview.Content.ReadAsStringAsync());

        Assert.Equal(
            expectedRules,
            previewJson.RootElement.GetProperty("rules").GetString());
    }
    private sealed record InvitationResponse(
        string Code,
        DateTime ExpiresAtUtc);
}
