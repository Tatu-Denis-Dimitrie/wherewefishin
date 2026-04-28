using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Enums;

namespace WhereWeFishin.Tests.Integration;

public class BookingsIntegrationTests
{
    [Fact]
    public async Task GetPaymentConfiguration_WhenStripeIsNotConfigured_ReturnsDisabled()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = await CreateAuthenticatedClientAsync(factory, "ion_fisher", "password123");

        var response = await client.GetAsync("/api/bookings/payment-configuration");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<PaymentConfigurationDto>();
        Assert.NotNull(payload);
        Assert.False(payload.StripeEnabled);
    }

    [Fact]
    public async Task CreateBooking_WithValidPayload_ReturnsCreatedAndAppearsInMyBookings()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = await CreateAuthenticatedClientAsync(factory, "ion_fisher", "password123");
        var spotId = await GetSpotIdAsync(factory, "Snagov Lake");
        var startDate = DateTime.UtcNow.AddHours(4);

        var createResponse = await client.PostAsJsonAsync("/api/bookings", new CreateBookingDto
        {
            FishingSpotId = spotId,
            StartDate = startDate,
            DurationHours = 2
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdBooking = await createResponse.Content.ReadFromJsonAsync<BookingDto>();
        Assert.NotNull(createdBooking);
        Assert.Equal(spotId, createdBooking!.FishingSpotId);
        Assert.Equal("Confirmed", createdBooking.Status);
        Assert.Equal(2, createdBooking.DurationHours);
        Assert.True(createdBooking.TotalPrice > 0);
        Assert.Equal(DateTimeKind.Utc, createdBooking.StartDate.Kind);
        Assert.Equal(startDate, createdBooking.StartDate, TimeSpan.FromSeconds(1));

        var listResponse = await client.GetAsync("/api/bookings");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var bookings = await listResponse.Content.ReadFromJsonAsync<List<BookingDto>>();
        Assert.NotNull(bookings);
        Assert.Contains(bookings, booking =>
            booking.Id == createdBooking.Id &&
            booking.FishingSpotName == "Snagov Lake" &&
            booking.StartDate.Kind == DateTimeKind.Utc &&
            booking.StartDate == startDate);
    }

    [Fact]
    public async Task CreateBooking_WithOverlappingInterval_ReturnsConflict()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = await CreateAuthenticatedClientAsync(factory, "ion_fisher", "password123");
        var spotId = await GetSpotIdAsync(factory, "Snagov Lake");
        var startDate = DateTime.UtcNow.AddHours(5);

        var firstResponse = await client.PostAsJsonAsync("/api/bookings", new CreateBookingDto
        {
            FishingSpotId = spotId,
            StartDate = startDate,
            DurationHours = 3
        });

        var secondResponse = await client.PostAsJsonAsync("/api/bookings", new CreateBookingDto
        {
            FishingSpotId = spotId,
            StartDate = startDate.AddHours(1),
            DurationHours = 2
        });

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task GetBookedPeriods_WhenBookingExists_ReturnsReservedIntervalForAnonymousCaller()
    {
        using var factory = new ApiWebApplicationFactory();
        var userClient = await CreateAuthenticatedClientAsync(factory, "ion_fisher", "password123");
        var anonymousClient = CreateClient(factory);
        var spotId = await GetSpotIdAsync(factory, "Snagov Lake");
        var startDate = DateTime.UtcNow.AddHours(6);

        await userClient.PostAsJsonAsync("/api/bookings", new CreateBookingDto
        {
            FishingSpotId = spotId,
            StartDate = startDate,
            DurationHours = 2
        });

        var response = await anonymousClient.GetAsync($"/api/bookings/booked-periods?spotId={spotId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var periods = await response.Content.ReadFromJsonAsync<List<BookedPeriodDto>>();
        Assert.NotNull(periods);
        var period = Assert.Single(periods);
        Assert.Equal(startDate.ToUniversalTime(), period.StartDate);
        Assert.Equal(startDate.ToUniversalTime().AddHours(2), period.EndDate);
    }

    [Fact]
    public async Task CancelBooking_WhenOwnerCancels_RemovesItFromBookedPeriods()
    {
        using var factory = new ApiWebApplicationFactory();
        var userClient = await CreateAuthenticatedClientAsync(factory, "ion_fisher", "password123");
        var anonymousClient = CreateClient(factory);
        var spotId = await GetSpotIdAsync(factory, "Snagov Lake");
        var startDate = DateTime.UtcNow.AddHours(7);

        var createResponse = await userClient.PostAsJsonAsync("/api/bookings", new CreateBookingDto
        {
            FishingSpotId = spotId,
            StartDate = startDate,
            DurationHours = 2
        });
        var booking = await createResponse.Content.ReadFromJsonAsync<BookingDto>();

        var cancelResponse = await userClient.DeleteAsync($"/api/bookings/{booking!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, cancelResponse.StatusCode);

        var periodsResponse = await anonymousClient.GetAsync($"/api/bookings/booked-periods?spotId={spotId}");
        var periods = await periodsResponse.Content.ReadFromJsonAsync<List<BookedPeriodDto>>();

        Assert.NotNull(periods);
        Assert.Empty(periods);

        var session = await factory.ExecuteDbContextAsync(context => context.FishingSessions.SingleAsync(current => current.Id == booking.Id));
        Assert.Equal(SessionStatus.Cancelled, session.Status);
    }

    [Fact]
    public async Task GetBooking_WhenRequestedByAdmin_ExposesVerificationTokenButHidesItFromOwner()
    {
        using var factory = new ApiWebApplicationFactory();
        var userClient = await CreateAuthenticatedClientAsync(factory, "ion_fisher", "password123");
        var adminClient = await CreateAuthenticatedClientAsync(factory, "admin", "admin123");
        var spotId = await GetSpotIdAsync(factory, "Snagov Lake");

        var createResponse = await userClient.PostAsJsonAsync("/api/bookings", new CreateBookingDto
        {
            FishingSpotId = spotId,
            StartDate = DateTime.UtcNow.AddHours(8),
            DurationHours = 2
        });
        var createdBooking = await createResponse.Content.ReadFromJsonAsync<BookingDto>();

        var ownerResponse = await userClient.GetAsync($"/api/bookings/{createdBooking!.Id}");
        var adminResponse = await adminClient.GetAsync($"/api/bookings/{createdBooking.Id}");

        Assert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);

        var ownerBooking = await ownerResponse.Content.ReadFromJsonAsync<BookingDto>();
        var adminBooking = await adminResponse.Content.ReadFromJsonAsync<BookingDto>();

        Assert.NotNull(ownerBooking);
        Assert.NotNull(adminBooking);
        Assert.Null(ownerBooking!.VerificationToken);
        Assert.False(string.IsNullOrWhiteSpace(adminBooking!.VerificationToken));
    }

    [Fact]
    public async Task GetAllBookings_WhenRequestedByAdmin_ReturnsCreatedReservation()
    {
        using var factory = new ApiWebApplicationFactory();
        var userClient = await CreateAuthenticatedClientAsync(factory, "ion_fisher", "password123");
        var adminClient = await CreateAuthenticatedClientAsync(factory, "admin", "admin123");
        var spotId = await GetSpotIdAsync(factory, "Snagov Lake");

        var createResponse = await userClient.PostAsJsonAsync("/api/bookings", new CreateBookingDto
        {
            FishingSpotId = spotId,
            StartDate = DateTime.UtcNow.AddHours(9),
            DurationHours = 2
        });
        var createdBooking = await createResponse.Content.ReadFromJsonAsync<BookingDto>();

        var response = await adminClient.GetAsync("/api/bookings/all");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var bookings = await response.Content.ReadFromJsonAsync<List<BookingDto>>();
        Assert.NotNull(bookings);
        Assert.Contains(bookings, booking => booking.Id == createdBooking!.Id && booking.VerificationToken is not null);
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

    private static Task<int> GetSpotIdAsync(ApiWebApplicationFactory factory, string spotName)
        => factory.ExecuteDbContextAsync(async context =>
            (await context.FishingSpots.SingleAsync(spot => spot.Name == spotName)).Id);
}