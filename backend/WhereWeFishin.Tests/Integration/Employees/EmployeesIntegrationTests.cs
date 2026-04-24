using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Enums;

namespace WhereWeFishin.Tests.Integration;

public class EmployeesIntegrationTests
{
    [Fact]
    public async Task AssignEmployee_AsManagerForOwnedSpot_CreatesAssignmentAndReturnsItFromSpotListing()
    {
        using var factory = new ApiWebApplicationFactory();
        var managerClient = await CreateAuthenticatedClientAsync(factory, "manager1", "manager123");
        var employee = await AddEmployeeAsync(factory, "assignable_employee", false);
        var spotId = await GetSpotIdAsync(factory, "Danube Delta");

        var assignResponse = await managerClient.PostAsJsonAsync("/api/employees", new AssignEmployeeDto
        {
            UserId = employee.Id,
            FishingSpotId = spotId
        });

        Assert.Equal(HttpStatusCode.Created, assignResponse.StatusCode);

        var getResponse = await managerClient.GetAsync($"/api/employees/spot/{spotId}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var assignments = await getResponse.Content.ReadFromJsonAsync<List<SpotEmployeeDto>>();
        Assert.NotNull(assignments);
        Assert.Contains(assignments, assignment => assignment.UserId == employee.Id && assignment.Username == employee.Username);
    }

    [Fact]
    public async Task AssignEmployee_WhenManagerDoesNotControlSpot_ReturnsForbid()
    {
        using var factory = new ApiWebApplicationFactory();
        var managerClient = await CreateAuthenticatedClientAsync(factory, "manager1", "manager123");
        var employee = await AddEmployeeAsync(factory, "forbidden_employee", false);
        var spotId = await GetSpotIdAsync(factory, "Bicaz Lake");

        var response = await managerClient.PostAsJsonAsync("/api/employees", new AssignEmployeeDto
        {
            UserId = employee.Id,
            FishingSpotId = spotId
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AssignEmployee_WhenAlreadyAssigned_ReturnsConflict()
    {
        using var factory = new ApiWebApplicationFactory();
        var managerClient = await CreateAuthenticatedClientAsync(factory, "manager1", "manager123");
        var employee = await AddEmployeeAsync(factory, "duplicate_employee", false);
        var spotId = await GetSpotIdAsync(factory, "Danube Delta");

        await managerClient.PostAsJsonAsync("/api/employees", new AssignEmployeeDto
        {
            UserId = employee.Id,
            FishingSpotId = spotId
        });

        var secondResponse = await managerClient.PostAsJsonAsync("/api/employees", new AssignEmployeeDto
        {
            UserId = employee.Id,
            FishingSpotId = spotId
        });

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task GetAvailableEmployees_ReturnsOnlyActiveEmployeeUsers()
    {
        using var factory = new ApiWebApplicationFactory();
        var managerClient = await CreateAuthenticatedClientAsync(factory, "manager1", "manager123");
        var activeEmployee = await AddEmployeeAsync(factory, "active_employee", false);
        await AddEmployeeAsync(factory, "deleted_employee", true);

        var response = await managerClient.GetAsync("/api/employees/available");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var employees = await response.Content.ReadFromJsonAsync<List<UserDto>>();
        Assert.NotNull(employees);
        Assert.Contains(employees, employee => employee.Id == activeEmployee.Id && employee.Role == Roles.Employee);
        Assert.DoesNotContain(employees, employee => employee.Username == "deleted_employee");
    }

    [Fact]
    public async Task GetMyAssignedSpots_AsEmployee_ReturnsManagerAssignment()
    {
        using var factory = new ApiWebApplicationFactory();
        var managerClient = await CreateAuthenticatedClientAsync(factory, "manager1", "manager123");
        var employee = await AddEmployeeAsync(factory, "assigned_employee", false);
        var spotId = await GetSpotIdAsync(factory, "Danube Delta");

        await managerClient.PostAsJsonAsync("/api/employees", new AssignEmployeeDto
        {
            UserId = employee.Id,
            FishingSpotId = spotId
        });

        var employeeClient = await CreateAuthenticatedClientAsync(factory, employee.Username, employee.Password);
        var response = await employeeClient.GetAsync("/api/employees/my-spots");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var assignments = await response.Content.ReadFromJsonAsync<List<SpotEmployeeDto>>();
        Assert.NotNull(assignments);
        Assert.Contains(assignments, assignment => assignment.FishingSpotId == spotId && assignment.FishingSpotName == "Danube Delta");
    }

    [Fact]
    public async Task VerifyQr_AsAssignedEmployee_ReturnsValidForActiveBooking()
    {
        using var factory = new ApiWebApplicationFactory();
        var employee = await AddEmployeeAsync(factory, "verifier_employee", false);
        var spotId = await GetSpotIdAsync(factory, "Danube Delta");
        var booking = await AddAssignmentAndActiveBookingAsync(factory, employee.Id, spotId, "valid-token");
        var employeeClient = await CreateAuthenticatedClientAsync(factory, employee.Username, employee.Password);

        var response = await employeeClient.PostAsJsonAsync("/api/employees/verify-qr", new VerifyQrDto
        {
            BookingId = booking.BookingId,
            VerificationToken = booking.VerificationToken
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<QrVerificationResultDto>();
        Assert.NotNull(payload);
        Assert.True(payload!.Valid);
        Assert.Equal("Danube Delta", payload.FishingSpotName);
        Assert.Equal(booking.BookingId, payload.BookingId);
        Assert.Equal("Confirmed", payload.Status);
    }

    [Fact]
    public async Task VerifyQr_AsUnassignedEmployee_ReturnsInvalidAssignmentMessage()
    {
        using var factory = new ApiWebApplicationFactory();
        var employee = await AddEmployeeAsync(factory, "unassigned_employee", false);
        var spotId = await GetSpotIdAsync(factory, "Danube Delta");
        var booking = await AddActiveBookingAsync(factory, spotId, "invalid-assignment-token");
        var employeeClient = await CreateAuthenticatedClientAsync(factory, employee.Username, employee.Password);

        var response = await employeeClient.PostAsJsonAsync("/api/employees/verify-qr", new VerifyQrDto
        {
            BookingId = booking.BookingId,
            VerificationToken = booking.VerificationToken
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<QrVerificationResultDto>();
        Assert.NotNull(payload);
        Assert.False(payload!.Valid);
        Assert.Equal("You are not assigned to this spot.", payload.Message);
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

    private static async Task<(int Id, string Username, string Password)> AddEmployeeAsync(
        ApiWebApplicationFactory factory,
        string username,
        bool isDeleted)
    {
        const string password = "Employee123!";

        return await factory.ExecuteDbContextAsync(async context =>
        {
            var user = new User
            {
                Username = username,
                Email = $"{username}@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                FirstName = "Employee",
                LastName = username,
                Role = UserRole.Employee,
                IsDeleted = isDeleted
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            return (user.Id, user.Username, password);
        });
    }

    private static Task<int> GetSpotIdAsync(ApiWebApplicationFactory factory, string spotName)
        => factory.ExecuteDbContextAsync(async context =>
            (await context.FishingSpots.SingleAsync(spot => spot.Name == spotName)).Id);

    private static async Task<(int BookingId, string VerificationToken)> AddAssignmentAndActiveBookingAsync(
        ApiWebApplicationFactory factory,
        int employeeId,
        int spotId,
        string token)
    {
        await factory.ExecuteDbContextAsync(async context =>
        {
            context.SpotEmployees.Add(new SpotEmployee
            {
                UserId = employeeId,
                FishingSpotId = spotId
            });

            await context.SaveChangesAsync();
            return 0;
        });

        return await AddActiveBookingAsync(factory, spotId, token);
    }

    private static Task<(int BookingId, string VerificationToken)> AddActiveBookingAsync(
        ApiWebApplicationFactory factory,
        int spotId,
        string token)
        => factory.ExecuteDbContextAsync(async context =>
        {
            var bookingUser = await context.Users.SingleAsync(user => user.Username == "ion_fisher");
            var session = new FishingSession
            {
                UserId = bookingUser.Id,
                FishingSpotId = spotId,
                StartDate = DateTime.UtcNow.AddMinutes(-30),
                DurationHours = 2,
                TotalPrice = 10,
                Status = SessionStatus.Confirmed,
                VerificationToken = token
            };

            context.FishingSessions.Add(session);
            await context.SaveChangesAsync();

            return (session.Id, token);
        });
}