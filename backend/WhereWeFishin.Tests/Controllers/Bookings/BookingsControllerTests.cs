using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Linq.Expressions;
using System.Security.Claims;
using WhereWeFishin.API.Controllers;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Enums;
using WhereWeFishin.Core.Interfaces;
using WhereWeFishin.Tests.TestHelpers;

namespace WhereWeFishin.Tests.Controllers;

public class BookingsControllerTests
{
    private readonly IRepository<FishingSession> _sessionRepository;
    private readonly IRepository<FishingSpot> _spotRepository;
    private readonly IRepository<Pontoon> _pontoonRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<BookingsController> _logger;
    private readonly IConfiguration _configuration;
    private readonly BookingsController _controller;

    public BookingsControllerTests()
    {
        _sessionRepository = Substitute.For<IRepository<FishingSession>>();
        _spotRepository = Substitute.For<IRepository<FishingSpot>>();
        _pontoonRepository = Substitute.For<IRepository<Pontoon>>();
        _userRepository = Substitute.For<IRepository<User>>();
        _emailService = Substitute.For<IEmailService>();
        _logger = Substitute.For<ILogger<BookingsController>>();
        _emailService.SendBookingConfirmationEmailAsync(
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<DateTime>(),
                Arg.Any<int>(),
                Arg.Any<decimal>(),
                Arg.Any<int>())
            .Returns(Task.CompletedTask);
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        _controller = new BookingsController(
            _sessionRepository,
            _spotRepository,
            _pontoonRepository,
            _userRepository,
            _emailService,
            _logger,
            _configuration);

        // Default: authenticated as user 1
        SetupUser(userId: 1, role: Roles.User);
    }

    private BookingsController CreateController(IConfiguration? configuration = null)
    {
        var controller = new BookingsController(
            _sessionRepository,
            _spotRepository,
            _pontoonRepository,
            _userRepository,
            _emailService,
            _logger,
            configuration ?? _configuration);

        controller.ControllerContext = _controller.ControllerContext;
        return controller;
    }

    private void SetupUser(int userId, string role = Roles.User)
    {
        ControllerContextFactory.SetAuthenticatedUser(_controller, userId, role);
    }

    private static FishingSpot CreateSpot(int id = 1, decimal pricePerHour = 10m, string name = "Test Spot") => new()
    {
        Id = id,
        Name = name,
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

    private static User CreateUser(int id = 1, string email = "testuser1@mail.com", string? firstName = null) => new()
    {
        Id = id,
        Username = $"testuser{id}",
        Email = email,
        FirstName = firstName,
        PasswordHash = "hash123"
    };


    [Fact]
    public async Task GetMyBookings_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        ControllerContextFactory.SetAnonymousUser(_controller);

        // Act
        var result = await _controller.GetMyBookings();

        // Assert
        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public void GetPaymentConfiguration_WhenStripeIsDisabled_ReturnsStripeDisabled()
    {
        // Act
        var result = _controller.GetPaymentConfiguration();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var configuration = Assert.IsType<PaymentConfigurationDto>(okResult.Value);
        Assert.False(configuration.StripeEnabled);
    }

    [Fact]
    public void GetPaymentConfiguration_WhenStripeIsEnabled_ReturnsStripeEnabled()
    {
        // Arrange
        var stripeConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Stripe:SecretKey"] = "sk_test_dummy"
            })
            .Build();
        var stripeController = CreateController(stripeConfig);

        // Act
        var result = stripeController.GetPaymentConfiguration();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var configuration = Assert.IsType<PaymentConfigurationDto>(okResult.Value);
        Assert.True(configuration.StripeEnabled);
    }

    [Fact]
    public async Task GetAllBookings_AsAdmin_ReturnsMappedBookingsWithVerificationToken()
    {
        // Arrange
        SetupUser(userId: 99, role: Roles.Admin);
        var firstSession = CreateSession(id: 1, userId: 1, spotId: 1);
        firstSession.CreatedAt = DateTime.UtcNow.AddMinutes(-15);
        firstSession.VerificationToken = "token-1";

        var secondSession = CreateSession(id: 2, userId: 2, spotId: 2);
        secondSession.PontoonId = 5;
        secondSession.CreatedAt = DateTime.UtcNow;
        secondSession.VerificationToken = "token-2";

        _sessionRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { firstSession, secondSession });
        _spotRepository.UseInMemoryStore(new[]
        {
            CreateSpot(id: 1, name: "River Spot"),
            CreateSpot(id: 2, name: "Lake Spot")
        });
        _pontoonRepository.UseInMemoryStore(new[]
        {
            CreatePontoon(id: 5, spotId: 2)
        });

        // Act
        var result = await _controller.GetAllBookings();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var bookings = Assert.IsAssignableFrom<IEnumerable<BookingDto>>(okResult.Value).ToList();
        Assert.Equal(2, bookings.Count);
        Assert.Equal(2, bookings[0].Id);
        Assert.Equal("Lake Spot", bookings[0].FishingSpotName);
        Assert.Equal("Pontoon 5", bookings[0].PontoonName);
        Assert.Equal("token-2", bookings[0].VerificationToken);
        Assert.Equal("River Spot", bookings[1].FishingSpotName);
    }


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
    public async Task CreateBooking_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        ControllerContextFactory.SetAnonymousUser(_controller);

        // Act
        var result = await _controller.CreateBooking(new CreateBookingDto
        {
            FishingSpotId = 1,
            StartDate = DateTime.UtcNow.AddDays(1),
            DurationHours = 24
        });

        // Assert
        Assert.IsType<UnauthorizedResult>(result.Result);
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
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-12)]
    [InlineData(8761)]
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
    public async Task CreateBooking_WhenStripeEnabledAndPaymentIntentMissing_ReturnsBadRequest()
    {
        // Arrange
        var stripeConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Stripe:SecretKey"] = "sk_test_dummy"
            })
            .Build();

        var stripeController = CreateController(stripeConfig);

        var spot = CreateSpot(1, pricePerHour: 10m);
        _spotRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(spot);
        _sessionRepository.FindAsync(Arg.Any<Expression<Func<FishingSession, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new List<FishingSession>());

        var dto = new CreateBookingDto
        {
            FishingSpotId = 1,
            StartDate = DateTime.UtcNow.AddDays(1),
            DurationHours = 24,
            PaymentIntentId = null
        };

        // Act
        var result = await stripeController.CreateBooking(dto);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateBooking_WhenClaimEmailIsMissing_FallsBackToUserRepository()
    {
        // Arrange
        ControllerContextFactory.SetAuthenticatedUser(_controller, userId: 1, email: string.Empty);
        var spot = CreateSpot(1, pricePerHour: 10m);
        var user = CreateUser(id: 1, email: "fallback@test.com", firstName: "Matei");

        _spotRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(spot);
        _sessionRepository.AddAsync(Arg.Any<FishingSession>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<FishingSession>());
        _userRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(user);

        // Act
        var result = await _controller.CreateBooking(new CreateBookingDto
        {
            FishingSpotId = 1,
            StartDate = DateTime.UtcNow.AddDays(1),
            DurationHours = 24
        });

        // Assert
        Assert.IsType<CreatedAtActionResult>(result.Result);
        await _emailService.Received(1).SendBookingConfirmationEmailAsync(
            "fallback@test.com",
            "Matei",
            "Test Spot",
            Arg.Any<DateTime>(),
            24,
            240m,
            Arg.Any<int>());
    }

    [Fact]
    public async Task CreateBooking_WhenSpotAlreadyBookedWithoutPontoon_ReturnsConflict()
    {
        // Arrange
        var spot = CreateSpot(1, pricePerHour: 10m);
        var start = DateTime.UtcNow.AddDays(1);
        var existingSession = CreateSession(id: 10, userId: 2, spotId: 1);
        existingSession.StartDate = start;
        existingSession.DurationHours = 24;
        existingSession.PontoonId = null;

        _spotRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(spot);
        _sessionRepository.FindAsync(Arg.Any<Expression<Func<FishingSession, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { existingSession });

        // Act
        var result = await _controller.CreateBooking(new CreateBookingDto
        {
            FishingSpotId = 1,
            StartDate = start,
            DurationHours = 24
        });

        // Assert
        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateBooking_WhenOnlyCancelledOverlapExists_ReturnsCreated()
    {
        // Arrange
        var spot = CreateSpot(1, pricePerHour: 10m);
        var start = DateTime.UtcNow.AddDays(1);
        var cancelledSession = CreateSession(id: 15, userId: 3, spotId: 1);
        cancelledSession.StartDate = start;
        cancelledSession.DurationHours = 24;
        cancelledSession.Status = SessionStatus.Cancelled;

        _spotRepository.UseInMemoryStore(new[] { spot });
        _sessionRepository.UseInMemoryStore(new[] { cancelledSession });

        // Act
        var result = await _controller.CreateBooking(new CreateBookingDto
        {
            FishingSpotId = 1,
            StartDate = start,
            DurationHours = 24
        });

        // Assert
        Assert.IsType<CreatedAtActionResult>(result.Result);
    }

    [Fact]
    public async Task CreatePaymentIntent_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var stripeConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Stripe:SecretKey"] = "sk_test_dummy"
            })
            .Build();
        var stripeController = CreateController(stripeConfig);
        ControllerContextFactory.SetAnonymousUser(stripeController);

        // Act
        var result = await stripeController.CreatePaymentIntent(new CreatePaymentIntentDto
        {
            FishingSpotId = 1,
            StartDate = DateTime.UtcNow.AddDays(1),
            DurationHours = 24
        });

        // Assert
        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task CreatePaymentIntent_WhenStripeIsDisabled_ReturnsServiceUnavailable()
    {
        // Act
        var result = await _controller.CreatePaymentIntent(new CreatePaymentIntentDto
        {
            FishingSpotId = 1,
            StartDate = DateTime.UtcNow.AddDays(1),
            DurationHours = 24
        });

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, statusResult.StatusCode);
    }

    [Fact]
    public async Task CreatePaymentIntent_WhenTotalPriceIsZero_ReturnsBadRequest()
    {
        // Arrange
        var stripeConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Stripe:SecretKey"] = "sk_test_dummy"
            })
            .Build();
        var stripeController = CreateController(stripeConfig);
        var spot = CreateSpot(1, pricePerHour: 0m);

        _spotRepository.UseInMemoryStore(new[] { spot });
        _sessionRepository.UseInMemoryStore<FishingSession>([]);

        // Act
        var result = await stripeController.CreatePaymentIntent(new CreatePaymentIntentDto
        {
            FishingSpotId = 1,
            StartDate = DateTime.UtcNow.AddDays(1),
            DurationHours = 24
        });

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
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
    public async Task GetMyBookings_WhenSpotLookupFails_UsesUnknownSpotName()
    {
        // Arrange
        var session = CreateSession(id: 1, userId: 1, spotId: 99);
        session.VerificationToken = "hidden-token";
        _sessionRepository.FindAsync(Arg.Any<Expression<Func<FishingSession, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { session });
        _spotRepository.UseInMemoryStore<FishingSpot>([]);

        // Act
        var result = await _controller.GetMyBookings();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var booking = Assert.Single(Assert.IsAssignableFrom<IEnumerable<BookingDto>>(okResult.Value));
        Assert.Equal("Unknown", booking.FishingSpotName);
        Assert.Null(booking.VerificationToken);
    }

    [Fact]
    public async Task GetBookedPeriods_WhenBothFiltersAreMissing_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetBookedPeriods(null, null);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetBookedPeriods_WithSpotId_ReturnsOnlyFutureSpotPeriodsOrdered()
    {
        // Arrange
        var now = DateTime.UtcNow;
        _sessionRepository.UseInMemoryStore(new[]
        {
            new FishingSession
            {
                Id = 1,
                UserId = 1,
                FishingSpotId = 1,
                StartDate = now.AddHours(4),
                DurationHours = 2,
                Status = SessionStatus.Confirmed
            },
            new FishingSession
            {
                Id = 2,
                UserId = 1,
                FishingSpotId = 1,
                StartDate = now.AddHours(1),
                DurationHours = 2,
                Status = SessionStatus.Pending
            },
            new FishingSession
            {
                Id = 3,
                UserId = 1,
                FishingSpotId = 1,
                StartDate = now.AddHours(-5),
                DurationHours = 1,
                Status = SessionStatus.Confirmed
            },
            new FishingSession
            {
                Id = 4,
                UserId = 1,
                FishingSpotId = 1,
                StartDate = now.AddHours(6),
                DurationHours = 2,
                Status = SessionStatus.Cancelled
            },
            new FishingSession
            {
                Id = 5,
                UserId = 1,
                FishingSpotId = 1,
                PontoonId = 11,
                StartDate = now.AddHours(3),
                DurationHours = 2,
                Status = SessionStatus.Confirmed
            }
        });

        // Act
        var result = await _controller.GetBookedPeriods(null, 1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var periods = Assert.IsAssignableFrom<IEnumerable<BookedPeriodDto>>(okResult.Value).ToList();
        Assert.Equal(2, periods.Count);
        Assert.True(periods[0].StartDate < periods[1].StartDate);
        Assert.Equal(now.AddHours(1), periods[0].StartDate, TimeSpan.FromSeconds(1));
        Assert.Equal(now.AddHours(4), periods[1].StartDate, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetBookedPeriods_WithPontoonId_ReturnsOnlyFuturePontoonPeriodsOrdered()
    {
        // Arrange
        var now = DateTime.UtcNow;
        _sessionRepository.UseInMemoryStore(new[]
        {
            new FishingSession
            {
                Id = 1,
                UserId = 1,
                FishingSpotId = 1,
                PontoonId = 7,
                StartDate = now.AddHours(5),
                DurationHours = 2,
                Status = SessionStatus.Confirmed
            },
            new FishingSession
            {
                Id = 2,
                UserId = 1,
                FishingSpotId = 1,
                PontoonId = 7,
                StartDate = now.AddHours(2),
                DurationHours = 2,
                Status = SessionStatus.Pending
            },
            new FishingSession
            {
                Id = 3,
                UserId = 1,
                FishingSpotId = 1,
                PontoonId = 7,
                StartDate = now.AddHours(-6),
                DurationHours = 1,
                Status = SessionStatus.Confirmed
            },
            new FishingSession
            {
                Id = 4,
                UserId = 1,
                FishingSpotId = 1,
                PontoonId = 8,
                StartDate = now.AddHours(3),
                DurationHours = 1,
                Status = SessionStatus.Confirmed
            }
        });

        // Act
        var result = await _controller.GetBookedPeriods(7, null);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var periods = Assert.IsAssignableFrom<IEnumerable<BookedPeriodDto>>(okResult.Value).ToList();
        Assert.Equal(2, periods.Count);
        Assert.True(periods[0].StartDate < periods[1].StartDate);
        Assert.Equal(now.AddHours(2), periods[0].StartDate, TimeSpan.FromSeconds(1));
        Assert.Equal(now.AddHours(5), periods[1].StartDate, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetBookedPeriods_WhenStoredDatesHaveUnspecifiedKind_ReturnsUtcPeriods()
    {
        // Arrange
        var now = DateTime.UtcNow;
        _sessionRepository.UseInMemoryStore(new[]
        {
            new FishingSession
            {
                Id = 1,
                UserId = 1,
                FishingSpotId = 1,
                StartDate = DateTime.SpecifyKind(now.AddHours(4), DateTimeKind.Unspecified),
                DurationHours = 2,
                Status = SessionStatus.Confirmed
            }
        });

        // Act
        var result = await _controller.GetBookedPeriods(null, 1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var periods = Assert.IsAssignableFrom<IEnumerable<BookedPeriodDto>>(okResult.Value).ToList();
        var period = Assert.Single(periods);
        Assert.Equal(DateTimeKind.Utc, period.StartDate.Kind);
        Assert.Equal(DateTimeKind.Utc, period.EndDate.Kind);
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
    public async Task GetBooking_WhenStoredStartDateHasUnspecifiedKind_ReturnsUtcDate()
    {
        // Arrange
        var expectedUtc = DateTime.UtcNow.AddDays(1);
        var session = CreateSession(1, userId: 1, spotId: 1);
        session.StartDate = DateTime.SpecifyKind(expectedUtc, DateTimeKind.Unspecified);
        _sessionRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(session);
        _spotRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(CreateSpot(1));

        // Act
        var result = await _controller.GetBooking(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var booking = Assert.IsType<BookingDto>(okResult.Value);
        Assert.Equal(DateTimeKind.Utc, booking.StartDate.Kind);
        Assert.Equal(expectedUtc, booking.StartDate, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetBooking_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        ControllerContextFactory.SetAnonymousUser(_controller);

        // Act
        var result = await _controller.GetBooking(1);

        // Assert
        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task GetBooking_WhenSpotIsMissing_UsesUnknownAndHidesVerificationToken()
    {
        // Arrange
        var session = CreateSession(1, userId: 1, spotId: 99);
        session.VerificationToken = "hidden-token";
        _sessionRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(session);
        _spotRepository.GetByIdAsync(99, Arg.Any<CancellationToken>()).Returns((FishingSpot?)null);

        // Act
        var result = await _controller.GetBooking(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var booking = Assert.IsType<BookingDto>(okResult.Value);
        Assert.Equal("Unknown", booking.FishingSpotName);
        Assert.Null(booking.VerificationToken);
    }

    [Fact]
    public async Task GetBooking_AsAdmin_IncludesVerificationToken()
    {
        // Arrange
        SetupUser(userId: 99, role: Roles.Admin);
        var session = CreateSession(1, userId: 2, spotId: 1);
        session.VerificationToken = "visible-token";
        _sessionRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(session);
        _spotRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(CreateSpot(1));

        // Act
        var result = await _controller.GetBooking(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var booking = Assert.IsType<BookingDto>(okResult.Value);
        Assert.Equal("visible-token", booking.VerificationToken);
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
        SetupUser(userId: 99, role: Roles.Admin);
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
    public async Task CancelBooking_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        ControllerContextFactory.SetAnonymousUser(_controller);

        // Act
        var result = await _controller.CancelBooking(1);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
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

    private static Pontoon CreatePontoon(int id = 1, int spotId = 1) => new()
    {
        Id = id,
        FishingSpotId = spotId,
        Name = $"Pontoon {id}",
        SouthWestLat = 44.9,
        SouthWestLng = 24.9,
        NorthEastLat = 45.1,
        NorthEastLng = 25.1
    };


    [Fact]
    public async Task CreateBooking_WithValidPontoon_ReturnsCreated()
    {
        // Arrange
        var spot = CreateSpot(1, pricePerHour: 10m);
        var pontoon = CreatePontoon(id: 1, spotId: 1);
        _spotRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(spot);
        _pontoonRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(pontoon);
        _sessionRepository.FindAsync(Arg.Any<Expression<Func<FishingSession, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new List<FishingSession>());
        _sessionRepository.AddAsync(Arg.Any<FishingSession>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<FishingSession>());

        var dto = new CreateBookingDto
        {
            FishingSpotId = 1,
            PontoonId = 1,
            StartDate = DateTime.UtcNow.AddDays(1),
            DurationHours = 24
        };

        // Act
        var result = await _controller.CreateBooking(dto);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var booking = Assert.IsType<BookingDto>(createdResult.Value);
        Assert.Equal(1, booking.PontoonId);
        Assert.Equal("Pontoon 1", booking.PontoonName);
        Assert.Equal(240m, booking.TotalPrice);
    }

    [Fact]
    public async Task CreateBooking_WithPontoonNotFound_ReturnsNotFound()
    {
        // Arrange
        var spot = CreateSpot(1);
        _spotRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(spot);
        _pontoonRepository.GetByIdAsync(99, Arg.Any<CancellationToken>()).Returns((Pontoon?)null);

        var dto = new CreateBookingDto
        {
            FishingSpotId = 1,
            PontoonId = 99,
            StartDate = DateTime.UtcNow.AddDays(1),
            DurationHours = 24
        };

        // Act
        var result = await _controller.CreateBooking(dto);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateBooking_WithPontoonFromWrongSpot_ReturnsBadRequest()
    {
        // Arrange – pontoon belongs to spot 2, but booking is for spot 1
        var spot = CreateSpot(1);
        var pontoon = CreatePontoon(id: 1, spotId: 2);
        _spotRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(spot);
        _pontoonRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(pontoon);

        var dto = new CreateBookingDto
        {
            FishingSpotId = 1,
            PontoonId = 1,
            StartDate = DateTime.UtcNow.AddDays(1),
            DurationHours = 24
        };

        // Act
        var result = await _controller.CreateBooking(dto);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateBooking_WithPontoonAlreadyBooked_ReturnsConflict()
    {
        // Arrange
        var spot = CreateSpot(1, pricePerHour: 10m);
        var pontoon = CreatePontoon(id: 1, spotId: 1);
        var start = DateTime.UtcNow.AddDays(1);
        var existingSession = new FishingSession
        {
            Id = 10,
            UserId = 2,
            FishingSpotId = 1,
            PontoonId = 1,
            StartDate = start,
            DurationHours = 24,
            TotalPrice = 240m,
            Status = SessionStatus.Confirmed
        };

        _spotRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(spot);
        _pontoonRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(pontoon);
        _sessionRepository.FindAsync(Arg.Any<Expression<Func<FishingSession, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new List<FishingSession> { existingSession });

        var dto = new CreateBookingDto
        {
            FishingSpotId = 1,
            PontoonId = 1,
            StartDate = start,
            DurationHours = 24
        };

        // Act
        var result = await _controller.CreateBooking(dto);

        // Assert
        Assert.IsType<ConflictObjectResult>(result.Result);
    }
}
