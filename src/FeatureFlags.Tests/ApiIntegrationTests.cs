using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using FeatureFlags.Api.DTOs;

namespace FeatureFlags.Tests;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection",
                "Server=(localdb)\\mssqllocaldb;Database=FeatureFlagsTest;Trusted_Connection=true");
        }).CreateClient();
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        var response = await _client.PostAsJsonAsync("/auth/login",
            new LoginRequest("admin@flagconsole.dev", "Admin@123!"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        body!.Token.Should().NotBeNullOrEmpty();
        body.Role.Should().Be("Admin");
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/auth/login",
            new LoginRequest("admin@flagconsole.dev", "wrongpassword"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetFlags_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/flags");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetFlags_WithAuth_ReturnsPagedList()
    {
        var token = await GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/flags");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<FlagListResponse>();
        body!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task EvaluateSnapshot_WithWrongApiKey_ReturnsUnauthorized()
    {
        var response = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/evaluate/dev/snapshot")
        {
            Headers = { { "X-Api-Key", "wrong-key" } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EvaluateSnapshot_ETagBehavior_Returns304OnSecondCall()
    {
        var request1 = new HttpRequestMessage(HttpMethod.Get, "/evaluate/dev/snapshot");
        request1.Headers.Add("X-Api-Key", "dev-api-key-00000000000000000001");
        var response1 = await _client.SendAsync(request1);
        response1.StatusCode.Should().Be(HttpStatusCode.OK);

        var etag = response1.Headers.ETag?.Tag;
        etag.Should().NotBeNullOrEmpty();

        var request2 = new HttpRequestMessage(HttpMethod.Get, "/evaluate/dev/snapshot");
        request2.Headers.Add("X-Api-Key", "dev-api-key-00000000000000000001");
        request2.Headers.Add("If-None-Match", etag!);
        var response2 = await _client.SendAsync(request2);
        response2.StatusCode.Should().Be(HttpStatusCode.NotModified);
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var resp = await _client.PostAsJsonAsync("/auth/login",
            new LoginRequest("admin@flagconsole.dev", "Admin@123!"));
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.Token;
    }
}
