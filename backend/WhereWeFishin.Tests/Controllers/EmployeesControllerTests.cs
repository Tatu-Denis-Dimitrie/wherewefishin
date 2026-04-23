using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using WhereWeFishin.API.Controllers;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Enums;
using WhereWeFishin.Core.Interfaces;
using WhereWeFishin.Tests.TestHelpers;

namespace WhereWeFishin.Tests.Controllers;

public class EmployeesControllerTests
{
    private readonly IRepository<SpotEmployee> _spotEmployeeRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<FishingSpot> _spotRepository;
    private readonly IRepository<FishingSession> _sessionRepository;
    private readonly IRepository<Pontoon> _pontoonRepository;
    private readonly EmployeesController _controller;
    private readonly List<SpotEmployee> _assignments;
    private readonly List<User> _users;
    private readonly List<FishingSpot> _spots;
    private readonly List<FishingSession> _sessions;
    private readonly List<Pontoon> _pontoons;

    public EmployeesControllerTests()
    {
        _spotEmployeeRepository = Substitute.For<IRepository<SpotEmployee>>();
        _userRepository = Substitute.For<IRepository<User>>();
        _spotRepository = Substitute.For<IRepository<FishingSpot>>();
        _sessionRepository = Substitute.For<IRepository<FishingSession>>();
        _pontoonRepository = Substitute.For<IRepository<Pontoon>>();

        _assignments = _spotEmployeeRepository.UseInMemoryStore<SpotEmployee>();
        _users = _userRepository.UseInMemoryStore<User>();
        _spots = _spotRepository.UseInMemoryStore<FishingSpot>();
        _sessions = _sessionRepository.UseInMemoryStore<FishingSession>();
        _pontoons = _pontoonRepository.UseInMemoryStore<Pontoon>();

        _controller = new EmployeesController(
            _spotEmployeeRepository,
            _userRepository,
            _spotRepository,
            _sessionRepository,
            _pontoonRepository);

        SetUser(1, Roles.Admin);
    }

    private void SetUser(int userId, string role)
    {
        ControllerContextFactory.SetAuthenticatedUser(_controller, userId, role);
    }

    private FishingSpot AddSpot(int id = 1, int ownerId = 1)
    {
        var spot = new FishingSpot
        {
            Id = id,
            Name = $"Spot {id}",
            Latitude = 45,
            Longitude = 25,
            PricePerHour = 10,
            UserId = ownerId
        };
        _spots.Add(spot);
        return spot;
    }

    private User AddUser(int id, UserRole role = UserRole.User, string? username = null)
    {
        var user = new User
        {
            Id = id,
            Username = username ?? $"user{id}",
            Email = $"user{id}@test.com",
            FirstName = $"First{id}",
            LastName = $"Last{id}",
            PasswordHash = "hash",
            Role = role
        };
        _users.Add(user);
        return user;
    }

    private SpotEmployee AddAssignment(int id, int userId, int spotId)
    {
        var assignment = new SpotEmployee
        {
            Id = id,
            UserId = userId,
            FishingSpotId = spotId,
            CreatedAt = DateTime.UtcNow
        };
        _assignments.Add(assignment);
        return assignment;
    }

    private FishingSession AddSession(int id, SessionStatus status, DateTime startDate, int durationHours = 12)
    {
        var session = new FishingSession
        {
            Id = id,
            UserId = 50,
            FishingSpotId = 1,
            StartDate = startDate,
            DurationHours = durationHours,
            TotalPrice = 120,
            Status = status,
            VerificationToken = $"token-{id}"
        };
        _sessions.Add(session);
        return session;
    }

    [Fact]
    public async Task GetSpotEmployees_WhenSpotMissing_ReturnsNotFound()
    {
        var result = await _controller.GetSpotEmployees(999);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetSpotEmployees_WhenManagerCannotManageSpot_ReturnsForbid()
    {
        AddSpot(1, ownerId: 10);
        SetUser(99, Roles.Manager);

        var result = await _controller.GetSpotEmployees(1);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task GetSpotEmployees_ReturnsMappedEmployees()
    {
        AddSpot(1, ownerId: 1);
        AddAssignment(1, 20, 1);
        AddAssignment(2, 21, 1);
        AddUser(20, UserRole.Employee, "employee20");
        AddUser(21, UserRole.Employee, "employee21");

        var result = await _controller.GetSpotEmployees(1);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var employees = Assert.IsAssignableFrom<IEnumerable<SpotEmployeeDto>>(okResult.Value).ToList();
        Assert.Equal(2, employees.Count);
        Assert.Contains(employees, employee => employee.Username == "employee20");
        Assert.All(employees, employee => Assert.Equal("Spot 1", employee.FishingSpotName));
    }

    [Fact]
    public async Task AssignEmployee_WhenSpotMissing_ReturnsNotFound()
    {
        var result = await _controller.AssignEmployee(new AssignEmployeeDto { UserId = 2, FishingSpotId = 1 });

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task AssignEmployee_WhenManagerCannotManageSpot_ReturnsForbid()
    {
        AddSpot(1, ownerId: 10);
        SetUser(99, Roles.Manager);

        var result = await _controller.AssignEmployee(new AssignEmployeeDto { UserId = 2, FishingSpotId = 1 });

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task AssignEmployee_WhenUserMissing_ReturnsNotFound()
    {
        AddSpot(1, ownerId: 1);

        var result = await _controller.AssignEmployee(new AssignEmployeeDto { UserId = 2, FishingSpotId = 1 });

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task AssignEmployee_WhenUserIsNotEmployee_ReturnsBadRequest()
    {
        AddSpot(1, ownerId: 1);
        AddUser(2, UserRole.User);

        var result = await _controller.AssignEmployee(new AssignEmployeeDto { UserId = 2, FishingSpotId = 1 });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task AssignEmployee_WhenAlreadyAssigned_ReturnsConflict()
    {
        AddSpot(1, ownerId: 1);
        AddUser(2, UserRole.Employee);
        AddAssignment(1, 2, 1);

        var result = await _controller.AssignEmployee(new AssignEmployeeDto { UserId = 2, FishingSpotId = 1 });

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task AssignEmployee_WithValidData_ReturnsCreated()
    {
        AddSpot(1, ownerId: 1);
        AddUser(2, UserRole.Employee, "employee2");

        var result = await _controller.AssignEmployee(new AssignEmployeeDto { UserId = 2, FishingSpotId = 1 });

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<SpotEmployeeDto>(createdResult.Value);
        Assert.True(dto.Id > 0);
        Assert.Equal("employee2", dto.Username);
        Assert.Equal("Spot 1", dto.FishingSpotName);
    }

    [Fact]
    public async Task RemoveEmployee_WhenMissing_ReturnsNotFound()
    {
        var result = await _controller.RemoveEmployee(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task RemoveEmployee_WhenManagerCannotManageSpot_ReturnsForbid()
    {
        AddSpot(1, ownerId: 10);
        AddAssignment(1, 2, 1);
        SetUser(99, Roles.Manager);

        var result = await _controller.RemoveEmployee(1);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task RemoveEmployee_WithValidData_ReturnsNoContent()
    {
        AddSpot(1, ownerId: 1);
        AddAssignment(1, 2, 1);

        var result = await _controller.RemoveEmployee(1);

        Assert.IsType<NoContentResult>(result);
        await _spotEmployeeRepository.Received(1).DeleteAsync(1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAvailableEmployees_ReturnsOnlyEmployeeUsers()
    {
        AddUser(1, UserRole.Employee);
        AddUser(2, UserRole.User);
        AddUser(3, UserRole.Employee);

        var result = await _controller.GetAvailableEmployees();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var employees = Assert.IsAssignableFrom<IEnumerable<UserDto>>(okResult.Value).ToList();
        Assert.Equal(2, employees.Count);
        Assert.All(employees, employee => Assert.Equal(Roles.Employee, employee.Role));
    }

    [Fact]
    public async Task GetMyAssignedSpots_WhenAnonymous_ReturnsUnauthorized()
    {
        ControllerContextFactory.SetAnonymousUser(_controller);

        var result = await _controller.GetMyAssignedSpots();

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task GetMyAssignedSpots_ReturnsAssignmentsWithUnknownSpotFallback()
    {
        SetUser(2, Roles.Employee);
        AddAssignment(1, 2, 1);
        AddAssignment(2, 2, 999);
        AddSpot(1, ownerId: 10);

        var result = await _controller.GetMyAssignedSpots();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var spots = Assert.IsAssignableFrom<IEnumerable<SpotEmployeeDto>>(okResult.Value).ToList();
        Assert.Equal(2, spots.Count);
        Assert.Contains(spots, spot => spot.FishingSpotName == "Spot 1");
        Assert.Contains(spots, spot => spot.FishingSpotName == "Unknown");
    }

    [Fact]
    public async Task VerifyQr_WhenAnonymous_ReturnsUnauthorized()
    {
        ControllerContextFactory.SetAnonymousUser(_controller);

        var result = await _controller.VerifyQr(new VerifyQrDto { BookingId = 1, VerificationToken = "token-1" });

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task VerifyQr_WhenBookingMissing_ReturnsInvalidResult()
    {
        SetUser(1, Roles.Employee);

        var result = await _controller.VerifyQr(new VerifyQrDto { BookingId = 1, VerificationToken = "token-1" });

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<QrVerificationResultDto>(okResult.Value);
        Assert.False(dto.Valid);
        Assert.Equal("Booking not found.", dto.Message);
    }

    [Fact]
    public async Task VerifyQr_WhenTokenIsInvalid_ReturnsInvalidResult()
    {
        SetUser(1, Roles.Employee);
        AddSession(1, SessionStatus.Confirmed, DateTime.UtcNow.AddHours(-1)).VerificationToken = "real-token";

        var result = await _controller.VerifyQr(new VerifyQrDto { BookingId = 1, VerificationToken = "wrong-token" });

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<QrVerificationResultDto>(okResult.Value);
        Assert.False(dto.Valid);
        Assert.Equal("Invalid QR code.", dto.Message);
    }

    [Fact]
    public async Task VerifyQr_WhenEmployeeIsNotAssigned_ReturnsInvalidResult()
    {
        SetUser(1, Roles.Employee);
        AddSpot(1, ownerId: 10);
        AddSession(1, SessionStatus.Confirmed, DateTime.UtcNow.AddHours(-1));

        var result = await _controller.VerifyQr(new VerifyQrDto { BookingId = 1, VerificationToken = "token-1" });

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<QrVerificationResultDto>(okResult.Value);
        Assert.False(dto.Valid);
        Assert.Equal("You are not assigned to this spot.", dto.Message);
    }

    [Fact]
    public async Task VerifyQr_WhenCancelled_ReturnsCancelledMessage()
    {
        SetUser(1, Roles.Manager);
        AddSpot(1, ownerId: 1);
        AddUser(50, UserRole.User, "angler50");
        AddSession(1, SessionStatus.Cancelled, DateTime.UtcNow.AddHours(-1));

        var result = await _controller.VerifyQr(new VerifyQrDto { BookingId = 1, VerificationToken = "token-1" });

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<QrVerificationResultDto>(okResult.Value);
        Assert.False(dto.Valid);
        Assert.Equal("Booking was cancelled.", dto.Message);
    }

    [Fact]
    public async Task VerifyQr_WhenExpired_ReturnsExpiredMessage()
    {
        SetUser(1, Roles.Manager);
        AddSpot(1, ownerId: 1);
        AddUser(50, UserRole.User, "angler50");
        AddSession(1, SessionStatus.Confirmed, DateTime.UtcNow.AddHours(-20), durationHours: 2);

        var result = await _controller.VerifyQr(new VerifyQrDto { BookingId = 1, VerificationToken = "token-1" });

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<QrVerificationResultDto>(okResult.Value);
        Assert.False(dto.Valid);
        Assert.Equal("Fishing session has expired.", dto.Message);
    }

    [Fact]
    public async Task VerifyQr_WhenNotStarted_ReturnsNotStartedMessage()
    {
        SetUser(1, Roles.Manager);
        AddSpot(1, ownerId: 1);
        AddUser(50, UserRole.User, "angler50");
        AddSession(1, SessionStatus.Confirmed, DateTime.UtcNow.AddHours(5));

        var result = await _controller.VerifyQr(new VerifyQrDto { BookingId = 1, VerificationToken = "token-1" });

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<QrVerificationResultDto>(okResult.Value);
        Assert.False(dto.Valid);
        Assert.Contains("Session has not started yet.", dto.Message);
    }

    [Fact]
    public async Task VerifyQr_WhenActiveAndEmployeeAssigned_ReturnsValidResult()
    {
        SetUser(1, Roles.Employee);
        AddSpot(1, ownerId: 10);
        AddAssignment(1, 1, 1);
        AddUser(50, UserRole.User, "angler50");
        _pontoons.Add(new Pontoon { Id = 7, FishingSpotId = 1, Name = "Pontoon 7" });
        var session = AddSession(1, SessionStatus.Confirmed, DateTime.UtcNow.AddHours(-1), durationHours: 5);
        session.PontoonId = 7;

        var result = await _controller.VerifyQr(new VerifyQrDto { BookingId = 1, VerificationToken = "token-1" });

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<QrVerificationResultDto>(okResult.Value);
        Assert.True(dto.Valid);
        Assert.Equal("Valid booking! Fishing session is active.", dto.Message);
        Assert.Equal("angler50", dto.Username);
        Assert.Equal("Spot 1", dto.FishingSpotName);
        Assert.Equal("Pontoon 7", dto.PontoonName);
        Assert.Equal(SessionStatus.Confirmed.ToString(), dto.Status);
    }
}