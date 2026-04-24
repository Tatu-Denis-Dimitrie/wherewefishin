using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Enums;

namespace WhereWeFishin.Tests.Integration;

public class AdminIntegrationTests
{
    [Fact]
    public async Task GetStats_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = CreateClient(factory);

        var response = await client.GetAsync("/api/admin/stats");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetStats_AsRegularUser_ReturnsForbidden()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = await CreateAuthenticatedClientAsync(factory, "ion_fisher", "password123");

        var response = await client.GetAsync("/api/admin/stats");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetStats_AsAdmin_ReturnsSeededSnapshot()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = await CreateAuthenticatedClientAsync(factory, "admin", "admin123");

        var response = await client.GetAsync("/api/admin/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<AdminStatsResponse>();
        Assert.NotNull(payload);
        Assert.Equal(10, payload.TotalUsers);
        Assert.Equal(2, payload.TotalManagers);
        Assert.Equal(1, payload.TotalAdmins);
        Assert.Equal(0, payload.DeactivatedUsers);
        Assert.Equal(4, payload.TotalSpots);
        Assert.Equal(0, payload.TotalBookings);
    }

    [Fact]
    public async Task GetAllUsers_AsAdmin_ReturnsSeededUsers()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = await CreateAuthenticatedClientAsync(factory, "admin", "admin123");

        var response = await client.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var users = await response.Content.ReadFromJsonAsync<List<UserDto>>();
        Assert.NotNull(users);
        Assert.Equal(10, users.Count);
        Assert.Contains(users, user => user.Username == "admin" && user.Role == Roles.Admin && user.IsActive);
        Assert.Contains(users, user => user.Username == "manager1" && user.Role == Roles.Manager);
    }

    [Fact]
    public async Task ToggleUserStatus_AsAdmin_DisablesUserAndBlocksSubsequentLogin()
    {
        using var factory = new ApiWebApplicationFactory();
        var adminClient = await CreateAuthenticatedClientAsync(factory, "admin", "admin123");
        var userId = await GetUserIdAsync(factory, "ion_fisher");

        var disableResponse = await adminClient.PutAsJsonAsync($"/api/admin/users/{userId}/status", new ToggleStatusDto
        {
            Enable = false
        });

        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);

        var disabledUser = await factory.ExecuteDbContextAsync(
            context => context.Users.IgnoreQueryFilters().SingleAsync(user => user.Id == userId));
        Assert.True(disabledUser.IsDeleted);

        var loginClient = CreateClient(factory);
        var loginResponse = await loginClient.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            UsernameOrEmail = "ion_fisher",
            Password = "password123"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateUserRole_AsAdmin_UpdatesStoredRole()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = await CreateAuthenticatedClientAsync(factory, "admin", "admin123");
        var userId = await GetUserIdAsync(factory, "maria_fisher");

        var response = await client.PutAsJsonAsync($"/api/admin/users/{userId}/role", new UpdateRoleDto
        {
            Role = Roles.Employee
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedUser = await factory.ExecuteDbContextAsync(
            context => context.Users.SingleAsync(user => user.Id == userId));
        Assert.Equal(UserRole.Employee, updatedUser.Role);
    }

    [Fact]
    public async Task DeleteFishingSpot_AsAdmin_SoftDeletesSpot()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = await CreateAuthenticatedClientAsync(factory, "admin", "admin123");
        var spotId = await GetSpotIdAsync(factory, "Bicaz Lake");

        var response = await client.DeleteAsync($"/api/admin/fishing-spots/{spotId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var deletedSpot = await factory.ExecuteDbContextAsync(
            context => context.FishingSpots.IgnoreQueryFilters().SingleAsync(spot => spot.Id == spotId));
        Assert.True(deletedSpot.IsDeleted);
    }

    private static HttpClient CreateClient(ApiWebApplicationFactory factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(
        ApiWebApplicationFactory factory,
        string usernameOrEmail,
        string password)
    {
        var client = CreateClient(factory);
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            UsernameOrEmail = usernameOrEmail,
            Password = password
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var authResponse = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authResponse);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authResponse!.Token);
        return client;
    }

    private static Task<int> GetUserIdAsync(ApiWebApplicationFactory factory, string username)
        => factory.ExecuteDbContextAsync(async context =>
            (await context.Users.IgnoreQueryFilters().SingleAsync(user => user.Username == username)).Id);

    private static Task<int> GetSpotIdAsync(ApiWebApplicationFactory factory, string name)
        => factory.ExecuteDbContextAsync(async context =>
            (await context.FishingSpots.IgnoreQueryFilters().SingleAsync(spot => spot.Name == name)).Id);

    private sealed class AdminStatsResponse
    {
        public int TotalUsers { get; set; }
        public int TotalManagers { get; set; }
        public int TotalAdmins { get; set; }
        public int DeactivatedUsers { get; set; }
        public int TotalAnalyses { get; set; }
        public int CompletedAnalyses { get; set; }
        public int FailedAnalyses { get; set; }
        public int TotalBookings { get; set; }
        public int ConfirmedBookings { get; set; }
        public int CancelledBookings { get; set; }
        public int TotalSpots { get; set; }
        public int TotalPontoons { get; set; }
        public int TotalReviews { get; set; }
    }
}