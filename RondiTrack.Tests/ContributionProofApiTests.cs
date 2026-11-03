using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Infrastructure;
using RondiTrack.Models.Dtos;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

[Collection("Postgres collection")]
public sealed class ContributionProofApiTests
{
    private readonly PostgresApiFactory _factory;
    public ContributionProofApiTests(PostgresApiFactory factory) => _factory = factory;

    private static readonly byte[] Pdf = "%PDF-1.4 test receipt"u8.ToArray();
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];

    private async Task<(HttpClient Client, Guid StokvelId, Guid ContributionId)> ArrangeAsync()
    {
        var client = _factory.CreateClient();
        var scenario = await client.ArrangeMemberWithCycleAsync(_factory);
        using var recorded = await client.PostContributionAsync(
            scenario.Stokvel.Id, Guid.NewGuid().ToString(), ApiTestHelpers.ContributionBody(scenario));
        Assert.Equal(HttpStatusCode.Created, recorded.StatusCode);
        var contribution = await recorded.Content.ReadFromJsonAsync<ContributionResponse>();
        Assert.NotNull(contribution);
        return (client, scenario.Stokvel.Id, contribution.Id);
    }

    private static string Url(Guid stokvelId, Guid contributionId) =>
        $"/api/v1/stokvels/{stokvelId}/contributions/{contributionId}/proof";

    private static MultipartFormDataContent UploadBody(byte[] bytes, string name, string contentType)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", name);
        return form;
    }

    [Theory]
    [InlineData("pdf")]
    [InlineData("jpeg")]
    [InlineData("png")]
    public async Task ValidReceipt_UploadsSuccessfully(string kind)
    {
        var s = await ArrangeAsync();
        using var client = s.Client;
        var bytes = kind == "pdf" ? Pdf : kind == "jpeg" ? Jpeg : Png;
        var type = kind == "pdf" ? "application/pdf" : kind == "jpeg" ? "image/jpeg" : "image/png";
        using var form = UploadBody(bytes, $"receipt.{kind}", type);
        using var response = await client.PostAsync(Url(s.StokvelId, s.ContributionId), form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task InvalidFileSignature_IsRejected()
    {
        var s = await ArrangeAsync();
        using var client = s.Client;
        using var form = UploadBody("not a pdf"u8.ToArray(), "receipt.pdf", "application/pdf");
        using var response = await client.PostAsync(Url(s.StokvelId, s.ContributionId), form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Receipt_CanBeReplaced()
    {
        var s = await ArrangeAsync();
        using var client = s.Client;
        using (var first = UploadBody(Pdf, "first.pdf", "application/pdf"))
        using (var response = await client.PostAsync(Url(s.StokvelId, s.ContributionId), first))
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var second = UploadBody(Png, "second.png", "image/png");
        using var replaced = await client.PostAsync(Url(s.StokvelId, s.ContributionId), second);
        Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);
        using var download = await client.GetAsync(Url(s.StokvelId, s.ContributionId));
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(Png, await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Download_IsPrivateAndReturnsOriginalBytes()
    {
        var s = await ArrangeAsync();
        using var client = s.Client;
        using var form = UploadBody(Pdf, "receipt.pdf", "application/pdf");
        using var uploaded = await client.PostAsync(Url(s.StokvelId, s.ContributionId), form);
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        using var response = await client.GetAsync(Url(s.StokvelId, s.ContributionId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Pdf, await response.Content.ReadAsByteArrayAsync());
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? "");
        Assert.True(response.Headers.TryGetValues("X-Content-Type-Options", out var values));
        Assert.Contains("nosniff", values!);
    }

    [Fact]
    public async Task Outsider_CannotDownloadReceipt()
    {
        var s = await ArrangeAsync();
        using var client = s.Client;
        using var form = UploadBody(Pdf, "receipt.pdf", "application/pdf");
        using var uploaded = await client.PostAsync(Url(s.StokvelId, s.ContributionId), form);
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        var outsider = await client.CreateUserAsync("Receipt Outsider");
        client.UseTestUser(outsider.Id);
        using var response = await client.GetAsync(Url(s.StokvelId, s.ContributionId));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Uploader_CanDeleteReceipt()
    {
        var s = await ArrangeAsync();
        using var client = s.Client;
        using var form = UploadBody(Pdf, "receipt.pdf", "application/pdf");
        using var uploaded = await client.PostAsync(Url(s.StokvelId, s.ContributionId), form);
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        using var deleted = await client.DeleteAsync(Url(s.StokvelId, s.ContributionId));
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var missing = await client.GetAsync(Url(s.StokvelId, s.ContributionId));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
    [Fact]
    public async Task OversizedReceipt_IsRejected()
    {
        var s = await ArrangeAsync();
        using var client = s.Client;
        var oversized = new byte[5 * 1024 * 1024 + 1];
        Array.Copy(Pdf, oversized, Pdf.Length);
        using var form = UploadBody(oversized, "large.pdf", "application/pdf");
        using var response = await client.PostAsync(Url(s.StokvelId, s.ContributionId), form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Treasurer_CanDownloadMembersReceipt()
    {
        var s = await ArrangeAsync();
        using var client = s.Client;
        using (var form = UploadBody(Pdf, "receipt.pdf", "application/pdf"))
        using (var uploaded = await client.PostAsync(Url(s.StokvelId, s.ContributionId), form))
            Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);

        var treasurer = await client.CreateUserAsync("Receipt Treasurer");
        await _factory.EnsureMembershipAsync(s.StokvelId, treasurer.Id);
        await _factory.SetMembershipRoleAsync(s.StokvelId, treasurer.Id, "Treasurer");
        client.UseTestUser(treasurer.Id);

        using var response = await client.GetAsync(Url(s.StokvelId, s.ContributionId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Pdf, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Chairperson_CannotDownloadAnotherMembersReceipt()
    {
        var s = await ArrangeAsync();
        using var client = s.Client;
        using (var form = UploadBody(Pdf, "receipt.pdf", "application/pdf"))
        using (var uploaded = await client.PostAsync(Url(s.StokvelId, s.ContributionId), form))
            Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);

        var chairperson = await client.CreateUserAsync("Receipt Chairperson");
        await _factory.EnsureMembershipAsync(s.StokvelId, chairperson.Id);
        await _factory.SetMembershipRoleAsync(s.StokvelId, chairperson.Id, "Chairperson");
        client.UseTestUser(chairperson.Id);

        using var response = await client.GetAsync(Url(s.StokvelId, s.ContributionId));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ClosedCycle_PreventsReceiptReplacement()
    {
        var s = await ArrangeAsync();
        using var client = s.Client;
        using (var form = UploadBody(Pdf, "original.pdf", "application/pdf"))
        using (var uploaded = await client.PostAsync(Url(s.StokvelId, s.ContributionId), form))
            Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);

        await CloseContributionCycleAsync(s.ContributionId);

        using var replacement = UploadBody(Png, "replacement.png", "image/png");
        using var response = await client.PostAsync(Url(s.StokvelId, s.ContributionId), replacement);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ClosedCycle_PreventsReceiptDeletion()
    {
        var s = await ArrangeAsync();
        using var client = s.Client;
        using (var form = UploadBody(Pdf, "receipt.pdf", "application/pdf"))
        using (var uploaded = await client.PostAsync(Url(s.StokvelId, s.ContributionId), form))
            Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);

        await CloseContributionCycleAsync(s.ContributionId);

        using var response = await client.DeleteAsync(Url(s.StokvelId, s.ContributionId));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task NearLimitReceipt_UploadsAndDownloadsSuccessfully()
    {
        var s = await ArrangeAsync();
        using var client = s.Client;

        var bytes = new byte[5 * 1024 * 1024 - 1024];
        Array.Copy(Pdf, bytes, Pdf.Length);

        using var form = UploadBody(bytes, "near-limit.pdf", "application/pdf");
        using var uploaded = await client.PostAsync(
            Url(s.StokvelId, s.ContributionId), form);

        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);

        using var download = await client.GetAsync(
            Url(s.StokvelId, s.ContributionId));

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(bytes, await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Outsider_CannotReadReceiptStatus()
    {
        var s = await ArrangeAsync();
        using var client = s.Client;

        using (var form = UploadBody(Pdf, "receipt.pdf", "application/pdf"))
        using (var uploaded = await client.PostAsync(
            Url(s.StokvelId, s.ContributionId), form))
        {
            Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        }

        var outsider = await client.CreateUserAsync("Receipt Status Outsider");
        client.UseTestUser(outsider.Id);

        using var response = await client.GetAsync(
            Url(s.StokvelId, s.ContributionId) + "/status");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Outsider_CannotDeleteAnotherMembersReceipt()
    {
        var s = await ArrangeAsync();
        using var client = s.Client;

        using (var form = UploadBody(Pdf, "receipt.pdf", "application/pdf"))
        using (var uploaded = await client.PostAsync(
            Url(s.StokvelId, s.ContributionId), form))
        {
            Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        }

        var outsider = await client.CreateUserAsync("Receipt Delete Outsider");
        client.UseTestUser(outsider.Id);

        using var response = await client.DeleteAsync(
            Url(s.StokvelId, s.ContributionId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Outsider_CannotReplaceReceiptAfterCycleCloses()
    {
        var s = await ArrangeAsync();
        using var client = s.Client;

        using (var form = UploadBody(Pdf, "receipt.pdf", "application/pdf"))
        using (var uploaded = await client.PostAsync(
            Url(s.StokvelId, s.ContributionId), form))
        {
            Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        }

        await CloseContributionCycleAsync(s.ContributionId);

        var outsider = await client.CreateUserAsync("Receipt Closed Outsider");
        client.UseTestUser(outsider.Id);

        using var replacement = UploadBody(
            Png, "replacement.png", "image/png");

        using var response = await client.PostAsync(
            Url(s.StokvelId, s.ContributionId), replacement);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    private async Task CloseContributionCycleAsync(Guid contributionId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        var cycleId = await db.Contributions
            .Where(c => c.Id == contributionId)
            .Select(c => c.ContributionCycleId)
            .SingleAsync();
        var cycle = await db.ContributionCycles.SingleAsync(c => c.Id == cycleId);
        cycle.Close(DateTime.UtcNow);
        await db.SaveChangesAsync();
    }
}
