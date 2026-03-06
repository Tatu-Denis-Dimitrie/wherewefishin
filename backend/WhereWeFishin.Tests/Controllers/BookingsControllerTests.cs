using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using System.Linq.Expressions;
using System.Security.Claims;
using WhereWeFishin.API.Controllers;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Interfaces;

namespace WhereWeFishin.Tests.Controllers;

public class BookingsControllerTests
{
    private readonly IRepository<FishingSession> _sessionRepository;
    private readonly IRepository<FishingSpot> _spotRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IEmailService _emailService;
    private readonly BookingsController _controller;

    public BookingsControllerTests()
    {
        _sessionRepository = Substitute.For<IRepository<FishingSession>>();
        _spotRepository = Substitute.For<IRepository<FishingSpot>>();
        _userRepository = Substitute.For<IRepository<User>>();
        _emailService = Substitute.For<IEmailService>();
        _emailService.SendBookingConfirmationEmailAsync(
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<DateTime>(),
                Arg.Any<int>(),
                Arg.Any<decimal>(),
                Arg.Any<int>())
            .Returns(Task.CompletedTask);

        _controller = new BookingsController(_sessionRepository, _spotRepository, _userRepository, _emailService);

        // Default: authenticated as user 1
        SetupUser(userId: 1, role: "User");
    }

    private void SetupUser(int userId, string role = "User")
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, $"testuser{userId}"),
            new Claim(ClaimTypes.Email, $"testuser{userId}@mail.com"),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var user = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    private static FishingSpot CreateSpot(int id = 1, decimal pricePerHour = 10m) => new()
    {
        Id = id,
        Name = "Test Spot",
        Latitude = 45.0,
        Longitude = 25.0,
        PricePerHour = pricePerHour,
        UserId = 1
    };

    private static FishingSession CreateSession(int id = 1, int userId = 1, int spotId = 1) => new()
    {
        Id = id,
        UserId = userId,
        FishingSpotId = spotId,
        StartDate = DateTime.UtcNow.AddDays(1),
        DurationHours = 24,
        TotalPrice = 240m,
        Status = SessionStatus.Confirmed
    };


    [Fact]
    public async Task CreateBooking_WithValidData_ReturnsCreated()
    {
        // Arrange
        var spot = CreateSpot(1, pricePerHour: 10m);
        _spotRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(spot);
        _sessionRepository.AddAsync(Arg.Any<FishingSession>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<FishingSession>());

        var dto = new CreateBookingDto
        {
            FishingSpotId = 1,
            StartDate = DateTime.UtcNow.AddDays(1),
            DurationHours = 24
        };

        // Act
        var result = await _controller.CreateBooking(dto);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var booking = Assert.IsType<BookingDto>(createdResult.Value);
        Assert.Equal(1, booking.UserId);
        Assert.Equal(1, booking.FishingSpotId);
        Assert.Equal(240m, booking.TotalPrice); // 10 * 24
        Assert.Equal("Confirmed", booking.Status);
        await _emailService.Received(1).SendBookingConfirmationEmailAsync(
            "testuser1@mail.com",
            Arg.Any<string?>(),
            "Test Spot",
            Arg.Any<DateTime>(),
            24,
            240m,
            Arg.Any<int>());
    }

    [Fact]
    public async Task CreateBooking_WhenEmailSendingFails_ReturnsCreated()
    {
        // Arrange
        var spot = CreateSpot(1, pricePerHour: 10m);
        _spotRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(spot);
        _sessionRepository.AddAsync(Arg.Any<FishingSession>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<FishingSession>());
        _emailService.SendBookingConfirmationEmailAsync(
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<DateTime>(),
                Arg.Any<int>(),
                Arg.Any<decimal>(),
                Arg.Any<int>())
            .Returns(Task.FromException(new InvalidOperationException("SMTP unavailable")));

        var dto = new CreateBookingDto
        {
            FishingSpotId = 1,
            StartDate = DateTime.UtcNow.AddDays(1),
            DurationHours = 24
        };

        // Act
        var result = await _controller.CreateBooking(dto);

        // Assert
        Assert.IsType<CreatedAtActionResult>(result.Result);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(24)]
    [InlineData(48)]
    [InlineData(72)]
    public async Task CreateBooking_WithAllValidDurations_ReturnsCreated(int durationHours)
    {
        // Arrange
        var spot = CreateSpot(1, pricePerHour: 5m);
        _spotRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(spot);
        _sessionRepository.AddAsync(Arg.Any<FishingSession>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<FishingSession>());

        var dto = new CreateBookingDto
        {
            FishingSpotId = 1,
            StartDate = DateTime.UtcNow.AddDays(1),
            DurationHours = durationHours
        };

        // Act
        var result = await _controller.CreateBooking(dto);

        // Assert
        Assert.IsType<CreatedAtActionResult>(result.Result);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(36)]
    [InlineData(100)]
    public async Task CreateBooking_WithInvalidDuration_ReturnsBadRequest(int invalidDuration)
    {
        // Arrange
        var spot = CreateSpot();
        _spotRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(spot);

        var dto = new CreateBookingDto
        {
            FishingSpotId = 1,
            StartDate = DateTime.UtcNow.AddDays(1),
            DurationHours = invalidDuration
        };

        // Act
        var result = await _controller.CreateBooking(dto);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task CreateBooking_WithPastStartDate_ReturnsBadRequest()
    {
        // Arrange
        var spot = CreateSpot();
        _spotRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(spot);

        var dto = new CreateBookingDto
        {
            FishingSpotId = 1,
            StartDate = DateTime.UtcNow.AddDays(-1), // past
            DurationHours = 24
        };

        // Act
        var result = await _controller.CreateBooking(dto);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateBooking_WhenSpotNotFound_ReturnsNotFound()
    {
        // Arrange
        _spotRepository.GetByIdAsync(999, Arg.Any<CancellationToken>()).Returns((FishingSpot?)null);

        var dto = new CreateBookingDto
        {
            FishingSpotId = 999,
            StartDate = DateTime.UtcNow.AddDays(1),
            DurationHours = 24
        };

        // Act
        var result = await _controller.CreateBooking(dto);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateBooking_CalculatesTotalPriceCorrectly()
    {
        // Arrange
        var spot = CreateSpot(1, pricePerHour: 7.5m);
        _spotRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(spot);
        _sessionRepository.AddAsync(Arg.Any<FishingSession>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<FishingSession>());

        var dto = new CreateBookingDto
        {
            FishingSpotId = 1,
            StartDate = DateTime.UtcNow.AddDays(1),
            DurationHours = 48
        };

        // Act
        var result = await _controller.CreateBooking(dto);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var booking = Assert.IsType<BookingDto>(createdResult.Value);
        Assert.Equal(360m, booking.TotalPrice); // 7.5 * 48
    }


    [Fact]
    public async Task GetMyBookings_ReturnsOnlyCurrentUserBookings()
    {
        // Arrange
        var sessions = new List<FishingSession> { CreateSession(1, userId: 1) };
        _sessionRepository
            .FindAsync(Arg.Any<Expression<Func<FishingSession, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(sessions);
        _spotRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<FishingSpot> { CreateSpot(1) });

        // Act
        var result = await _controller.GetMyBookings();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var bookings = Assert.IsAssignableFrom<IEnumerable<BookingDto>>(okResult.Value);
        Assert.Single(bookings);
    }


    [Fact]
    public async Task GetBooking_WhenOwner_ReturnsBooking()
    {
        // Arrange
        var session = CreateSession(1, userId: 1, spotId: 1);
        var spot = CreateSpot(1);
        _sessionRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(session);
        _spotRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(spot);

        // Act
        var result = await _controller.GetBooking(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var booking = Assert.IsType<BookingDto>(okResult.Value);
        Assert.Equal(1, booking.Id);
    }

    [Fact]
    public async Task GetBooking_WhenNotOwner_ReturnsForbid()
    {
        // Arrange – session belongs to user 2, current user is 1
        var session = CreateSession(1, userId: 2, spotId: 1);
        _sessionRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(session);

        // Act
        var result = await _controller.GetBooking(1);

        // Assert
        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task GetBooking_AsAdmin_CanAccessOtherUsersBooking()
    {
        // Arrange – session belongs to user 2, current user is admin
        SetupUser(userId: 99, role: "Admin");
        var session = CreateSession(1, userId: 2, spotId: 1);
        var spot = CreateSpot(1);
        _sessionRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(session);
        _spotRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(spot);

        // Act
        var result = await _controller.GetBooking(1);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetBooking_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        _sessionRepository.GetByIdAsync(999, Arg.Any<CancellationToken>()).Returns((FishingSession?)null);

        // Act
        var result = await _controller.GetBooking(999);

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }


    [Fact]
    public async Task CancelBooking_WhenOwnerAndConfirmed_ReturnsNoContent()
    {
        // Arrange
        var session = CreateSession(1, userId: 1);
        _sessionRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(session);

        // Act
        var result = await _controller.CancelBooking(1);

        // Assert
        Assert.IsType<NoContentResult>(result);
        await _sessionRepository.Received(1).UpdateAsync(
            Arg.Is<FishingSession>(s => s.Status == SessionStatus.Cancelled),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelBooking_WhenAlreadyCancelled_ReturnsBadRequest()
    {
        // Arrange
        var session = CreateSession(1, userId: 1);
        session.Status = SessionStatus.Cancelled;
        _sessionRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(session);

        // Act
        var result = await _controller.CancelBooking(1);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CancelBooking_WhenNotOwner_ReturnsForbid()
    {
        // Arrange – session belongs to user 2
        var session = CreateSession(1, userId: 2);
        _sessionRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(session);

        // Act
        var result = await _controller.CancelBooking(1);

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task CancelBooking_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        _sessionRepository.GetByIdAsync(999, Arg.Any<CancellationToken>()).Returns((FishingSession?)null);

        // Act
        var result = await _controller.CancelBooking(999);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }
}
