using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;

namespace WhereWeFishin.Tests.Integration;

public class AuthIntegrationTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public AuthIntegrationTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient()
        => _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

    private static RegisterRequest CreateRegisterRequest(string suffix) => new()
    {
        Username = $"integration_{suffix}",
        Email = $"integration_{suffix}@test.com",
        Password = "Password123!",
        ConfirmPassword = "Password123!",
        FirstName = "Test",
        LastName = "User"
    };

    [Fact]
    public async Task Register_WithValidPayload_ReturnsCreatedAndPersistsUser()
    {
        var client = CreateClient();
        var request = CreateRegisterRequest(Guid.NewGuid().ToString("N"));

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(payload);
        Assert.Equal(request.Username, payload.Username);
        Assert.Equal(request.Email, payload.Email);
        Assert.False(string.IsNullOrWhiteSpace(payload.Token));

        var storedUser = await _factory.ExecuteDbContextAsync(
            context => context.Users.SingleAsync(user => user.Username == request.Username));

        Assert.Equal(request.Email, storedUser.Email);
        Assert.NotEqual(request.Password, storedUser.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify(request.Password, storedUser.PasswordHash));
    }

    [Fact]
    public async Task Register_WithDuplicateIdentity_ReturnsConflict()
    {
        var client = CreateClient();
        var request = CreateRegisterRequest(Guid.NewGuid().ToString("N"));

        var firstResponse = await client.PostAsJsonAsync("/api/auth/register", request);
        var secondResponse = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task Login_AfterRegister_ReturnsJwtToken()
    {
        var client = CreateClient();
        var request = CreateRegisterRequest(Guid.NewGuid().ToString("N"));
        await client.PostAsJsonAsync("/api/auth/register", request);

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            UsernameOrEmail = request.Email,
            Password = request.Password
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(payload);
        Assert.Equal(request.Username, payload.Username);
        Assert.False(string.IsNullOrWhiteSpace(payload.Token));
    }

    [Fact]
    public async Task Verify_WithoutToken_ReturnsUnauthorized()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/auth/verify");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Verify_WithBearerToken_ReturnsClaimsPayload()
    {
        var client = CreateClient();
        var request = CreateRegisterRequest(Guid.NewGuid().ToString("N"));
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", request);
        var authPayload = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authPayload!.Token);

        var response = await client.GetAsync("/api/auth/verify");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var verifyPayload = await response.Content.ReadFromJsonAsync<VerifyTokenResponse>();
        Assert.NotNull(verifyPayload);
        Assert.Equal(request.Username, verifyPayload.Username);
        Assert.Equal(request.Email, verifyPayload.Email);
        Assert.False(string.IsNullOrWhiteSpace(verifyPayload.UserId));
    }

    [Fact]
    public async Task Register_WithInvalidPayload_ReturnsBadRequest()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = "ab",
            Email = "invalid-email",
            Password = "123",
            ConfirmPassword = "456"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed class VerifyTokenResponse
    {
        public string? Message { get; set; }
        public string? UserId { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
    }
}