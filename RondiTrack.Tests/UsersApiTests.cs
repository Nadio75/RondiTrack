namespace RondiTrack.Tests;

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RondiTrack.Models;

public class UsersApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public UsersApiTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();

    // ---------- Happy paths ----------

    [Fact]
    public async Task Creating_a_user_returns_201_with_a_location_and_the_user_can_then_be_fetched()
    {
        var response = await _client.PostAsJsonAsync("/api/users",
            new { name = "Nomsa Khumalo", contactNumber = "0829998888" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(created);
        Assert.EndsWith($"/api/users/{created!.Id}", response.Headers.Location!.ToString());

        var fetched = await _client.GetFromJsonAsync<UserResponse>($"/api/users/{created.Id}");
        Assert.Equal(created, fetched);
    }

    [Fact]
    public async Task Listing_users_includes_a_user_that_was_just_created()
    {
        var user = await _client.CreateUserAsync();

        var users = await _client.GetFromJsonAsync<List<UserResponse>>("/api/users");

        Assert.Contains(users!, u => u.Id == user.Id);
    }

    [Fact]
    public async Task Updating_a_user_replaces_both_name_and_contact_number()
    {
        var user = await _client.CreateUserAsync();

        var response = await _client.PutAsJsonAsync($"/api/users/{user.Id}",
            new { name = "Renamed User", contactNumber = "0711112222" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal(user.Id, updated!.Id);
        Assert.Equal("Renamed User", updated.Name);
        Assert.Equal("0711112222", updated.ContactNumber);
    }

    [Fact]
    public async Task Deleting_a_user_returns_204_and_the_user_is_then_gone()
    {
        var user = await _client.CreateUserAsync();

        var deleted = await _client.DeleteAsync($"/api/users/{user.Id}");
        var fetched = await _client.GetAsync($"/api/users/{user.Id}");
        var deletedAgain = await _client.DeleteAsync($"/api/users/{user.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        fetched.AssertProblem(HttpStatusCode.NotFound);
        deletedAgain.AssertProblem(HttpStatusCode.NotFound);
    }

    // ---------- Validation failures (400) ----------

    [Fact]
    public async Task Creating_a_user_with_a_contact_number_that_is_not_10_digits_returns_400_and_says_why()
    {
        var response = await _client.PostAsJsonAsync("/api/users",
            new { name = "Test User", contactNumber = "12345" });

        response.AssertProblem(HttpStatusCode.BadRequest);
        Assert.Contains("10 digits", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Creating_a_user_with_a_blank_name_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/users",
            new { name = "   ", contactNumber = "0821234567" });

        response.AssertProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Updating_with_an_invalid_body_returns_400_even_when_the_user_does_not_exist()
    {
        // Validation runs before the lookup, exactly as the docs promise.
        var response = await _client.PutAsJsonAsync($"/api/users/{Guid.NewGuid()}",
            new { name = "", contactNumber = "abc" });

        response.AssertProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Fetching_a_user_with_an_id_that_is_not_a_guid_returns_400()
    {
        var response = await _client.GetAsync("/api/users/not-a-guid");

        response.AssertProblem(HttpStatusCode.BadRequest);
    }

    // ---------- Not found (404) ----------

    [Fact]
    public async Task Fetching_a_user_that_does_not_exist_returns_404()
    {
        var response = await _client.GetAsync($"/api/users/{Guid.NewGuid()}");

        response.AssertProblem(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Updating_a_user_that_does_not_exist_returns_404()
    {
        var response = await _client.PutAsJsonAsync($"/api/users/{Guid.NewGuid()}",
            new { name = "Valid Name", contactNumber = "0821234567" });

        response.AssertProblem(HttpStatusCode.NotFound);
    }
}