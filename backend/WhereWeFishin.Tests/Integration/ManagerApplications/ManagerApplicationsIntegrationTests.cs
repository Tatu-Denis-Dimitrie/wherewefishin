using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Enums;

namespace WhereWeFishin.Tests.Integration;

public class ManagerApplicationsIntegrationTests
{
    [Fact]
    public async Task Create_AsUser_ReturnsCreatedAndAppearsInMine()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = await CreateAuthenticatedClientAsync(factory, "ion_fisher", "password123");

        var createResponse = await client.PostAsJsonAsync("/api/managerapplications", BuildRequest("Cerna Valley Lake"));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var mineResponse = await client.GetAsync("/api/managerapplications/mine");
        Assert.Equal(HttpStatusCode.OK, mineResponse.StatusCode);

        var applications = await mineResponse.Content.ReadFromJsonAsync<List<ManagerApplicationDto>>();
        Assert.NotNull(applications);
        Assert.Contains(applications, application => application.LakeName == "Cerna Valley Lake" && application.Status == ManagerApplicationStatus.Pending.ToString());
    }

    [Fact]
    public async Task RejectAndResubmit_Flow_UpdatesStatusAndClearsReason()
    {
        using var factory = new ApiWebApplicationFactory();
        var userClient = await CreateAuthenticatedClientAsync(factory, "maria_fisher", "password123");
        var adminClient = await CreateAuthenticatedClientAsync(factory, "admin", "admin123");

        var createResponse = await userClient.PostAsJsonAsync("/api/managerapplications", BuildRequest("Reject Flow Lake"));
        var created = await createResponse.Content.ReadFromJsonAsync<ManagerApplicationDto>();

        var rejectResponse = await adminClient.PostAsJsonAsync($"/api/managerapplications/{created!.Id}/reject", new RejectManagerApplicationDto
        {
            Reason = "Te rog completează mai clar baza de administrare."
        });

        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);

        var updateResponse = await userClient.PutAsJsonAsync($"/api/managerapplications/{created.Id}", new UpdateManagerApplicationDto
        {
            LakeName = "Reject Flow Lake",
            Description = "Updated details",
            Latitude = 45.123,
            Longitude = 25.456,
            LocationLabel = "Updated location",
            ProposedPricePerHour = 40,
            FishSpecies = "[\"Carp\"]",
            ContactPhone = "0711111111",
            Motivation = "Updated motivation",
            AdministrationBasis = "Concession contract"
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var resubmitResponse = await userClient.PostAsync($"/api/managerapplications/{created.Id}/resubmit", JsonContent.Create(new { }));
        Assert.Equal(HttpStatusCode.OK, resubmitResponse.StatusCode);

        var application = await factory.ExecuteDbContextAsync(context =>
            context.ManagerApplications.IgnoreQueryFilters().SingleAsync(item => item.Id == created.Id));

        Assert.Equal(ManagerApplicationStatus.Pending, application.Status);
        Assert.Null(application.RejectionReason);
    }

    [Fact]
    public async Task Approve_AsAdmin_PromotesUserCreatesSpotAndExposesItPublicly()
    {
        using var factory = new ApiWebApplicationFactory();
        var userClient = await CreateAuthenticatedClientAsync(factory, "ion_fisher", "password123");
        var adminClient = await CreateAuthenticatedClientAsync(factory, "admin", "admin123");

        var createResponse = await userClient.PostAsJsonAsync("/api/managerapplications", BuildRequest("Approval Lake"));
        var created = await createResponse.Content.ReadFromJsonAsync<ManagerApplicationDto>();

        var approveResponse = await adminClient.PostAsync($"/api/managerapplications/{created!.Id}/approve", JsonContent.Create(new { }));
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        var updatedUser = await factory.ExecuteDbContextAsync(context =>
            context.Users.SingleAsync(user => user.Username == "ion_fisher"));
        var createdSpot = await factory.ExecuteDbContextAsync(context =>
            context.FishingSpots.SingleAsync(spot => spot.Name == "Approval Lake"));

        Assert.Equal(UserRole.Manager, updatedUser.Role);
        Assert.Equal(updatedUser.Id, createdSpot.ManagerId);

        var publicClient = CreateClient(factory);
        var publicSpotsResponse = await publicClient.GetAsync("/api/fishingspots");
        Assert.Equal(HttpStatusCode.OK, publicSpotsResponse.StatusCode);

        var spots = await publicSpotsResponse.Content.ReadFromJsonAsync<List<FishingSpotDto>>();
        Assert.NotNull(spots);
        Assert.Contains(spots, spot => spot.Name == "Approval Lake");
    }

    [Fact]
    public async Task GetHomeOverview_AsAdmin_ReturnsPendingApplications()
    {
        using var factory = new ApiWebApplicationFactory();
        var userClient = await CreateAuthenticatedClientAsync(factory, "maria_fisher", "password123");
        var adminClient = await CreateAuthenticatedClientAsync(factory, "admin", "admin123");

        await userClient.PostAsJsonAsync("/api/managerapplications", BuildRequest("Overview Lake"));

        var response = await adminClient.GetAsync("/api/admin/home-overview");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var overview = await response.Content.ReadFromJsonAsync<AdminHomeOverviewDto>();
        Assert.NotNull(overview);
        Assert.True(overview.PendingManagerApplications >= 1);
        Assert.Contains(overview.PendingApplications, application => application.LakeName == "Overview Lake");
    }

    [Fact]
    public async Task CreateFishingSpot_AsManager_ReturnsForbidden()
    {
        using var factory = new ApiWebApplicationFactory();
        var managerClient = await CreateAuthenticatedClientAsync(factory, "manager1", "manager123");

        var response = await managerClient.PostAsJsonAsync("/api/fishingspots", new CreateFishingSpotDto
        {
            Name = "Forbidden Direct Lake",
            Latitude = 45.5,
            Longitude = 26.5,
            PricePerHour = 20
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static CreateManagerApplicationDto BuildRequest(string lakeName)
        => new()
        {
            LakeName = lakeName,
            Description = "A lake proposal created during integration tests.",
            Latitude = 45.123456,
            Longitude = 25.654321,
            LocationLabel = "Test county, Test city",
            ProposedPricePerHour = 30,
            FishSpecies = "[\"Carp\",\"Pike\"]",
            ContactPhone = "0712345678",
            Motivation = "I can manage the lake and coordinate bookings.",
            AdministrationBasis = "Owner"
        };

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
}