using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NexusHR.BuildingBlocks.Hosting;
using NexusHR.Gateway.CandidateOverview;

namespace NexusHR.Platform.Tests;

public sealed class GatewayRegressionTests
{
    [Fact]
    public async Task UnfilteredPageDoesNotReadEveryCandidateAndOnlyEnrichesTheRequestedIds()
    {
        var ids = Enumerable.Range(0, 20).Select(_ => Guid.NewGuid()).ToArray();
        var candidateCalls = 0; var hiringCalls = 0;
        await using var fixture = await HostAsync(async request => {
            if (request.RequestUri!.AbsolutePath == "/api/candidates") {
                candidateCalls++; Assert.Contains("page=3&pageSize=20", request.RequestUri.Query);
                Assert.Equal("Bearer synthetic-token", request.Headers.Authorization!.ToString());
                return Json(new CandidatePageResponse(ids.Select(Item).ToArray(), 3, 20, 100000));
            }
            hiringCalls++; var submitted = await request.Content!.ReadFromJsonAsync<CandidateIdsRequest>();
            Assert.Equal(ids, submitted!.CandidateIds);
            return Json(new[] { new CandidateHiringProcessStatusResponse(ids[0], Guid.NewGuid(), "OfferSent") });
        });
        fixture.Client.DefaultRequestHeaders.Authorization = new("Bearer", "synthetic-token");
        var result = await fixture.Client.GetFromJsonAsync<CandidateOverviewResponse>("/api/candidate-overview?page=3&pageSize=20");
        Assert.Equal(100000, result!.TotalCount); Assert.Equal(20, result.Items.Count);
        Assert.Equal("OfferSent", result.Items.First().DisplayStatus); Assert.Equal(ids, result.Items.Select(x => x.Id));
        Assert.Equal(1, candidateCalls); Assert.Equal(1, hiringCalls);
    }

    [Fact]
    public async Task ExtremeFilteredPageIsEmptyInsteadOfWrappingBackToFirstPage()
    {
        var id = Guid.NewGuid();
        await using var fixture = await HostAsync(request => Task.FromResult(request.Method == HttpMethod.Get
            ? Json(new CandidatePageResponse([Item(id)], 1, 100, 1)) : Json(Array.Empty<CandidateHiringProcessStatusResponse>())));
        var result = await fixture.Client.GetFromJsonAsync<CandidateOverviewResponse>("/api/candidate-overview?page=2147483647&pageSize=100&status=candidate:Draft");
        Assert.Empty(result!.Items); Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task BroadCrossServiceFilterFailsExplicitlyWithoutTruncatingOrScanningEverything()
    {
        var calls = 0;
        await using var fixture = await HostAsync(_ => { calls++; return Task.FromResult(Json(new CandidatePageResponse([Item(Guid.NewGuid())], 1, 100, 5001))); });
        using var response = await fixture.Client.GetAsync("/api/candidate-overview?page=1&pageSize=20&status=hiring:OfferSent");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.Equal(1, calls);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("CandidateOverview.ValidationFailed", problem.GetProperty("code").GetString());
        Assert.True(problem.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task DownstreamErrorsDoNotRetainSensitiveResponseBodiesAndHttpContractIsProblemDetails()
    {
        const string secret = "synthetic-personal-information-never-retain";
        var factory = new Clients(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent(secret) }));
        var error = await Assert.ThrowsAsync<CandidateOverviewDownstreamException>(() =>
            new CandidateOverviewService(factory).GetAsync(1, 20, null, null, null, default));
        Assert.DoesNotContain(secret, error.ToString());
        await using var fixture = await HostAsync(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent(secret) }));
        using var response = await fixture.Client.GetAsync("/api/candidate-overview?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.DoesNotContain(secret, await response.Content.ReadAsStringAsync());
    }

    private static CandidateItemResponse Item(Guid id) => new(id, "Synthetic", "Person", "synthetic@example.test", "0000000000", "Draft", DateTime.UnixEpoch);
    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };

    private static async Task<Fixture> HostAsync(Func<HttpRequestMessage, Task<HttpResponseMessage>> reply)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer(); builder.AddApiDefaults();
        builder.Services.AddAuthorization(o => o.AddPolicy("fixture", p => p.RequireAssertion(_ => true)));
        builder.Services.AddSingleton<IHttpClientFactory>(new Clients(reply)); builder.Services.AddScoped<CandidateOverviewService>();
        var app = builder.Build(); app.UseApiDefaults(); app.UseAuthorization(); app.MapCandidateOverviewEndpoints("fixture");
        await app.StartAsync(); return new(app, app.GetTestClient());
    }
    private sealed record Fixture(WebApplication App, HttpClient Client) : IAsyncDisposable
    { public async ValueTask DisposeAsync() { Client.Dispose(); await App.DisposeAsync(); } }
    private sealed class Clients(Func<HttpRequestMessage, Task<HttpResponseMessage>> reply) : IHttpClientFactory
    { public HttpClient CreateClient(string name) => new(new Reply(reply)) { BaseAddress = new Uri("http://isolated-downstream") }; }
    private sealed class Reply(Func<HttpRequestMessage, Task<HttpResponseMessage>> reply) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) { ct.ThrowIfCancellationRequested(); return reply(request); } }
}
