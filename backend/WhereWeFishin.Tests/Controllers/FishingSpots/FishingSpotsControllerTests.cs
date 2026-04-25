using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using NSubstitute;
using WhereWeFishin.API.Controllers;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Enums;
using WhereWeFishin.Core.Interfaces;
using WhereWeFishin.Tests.TestHelpers;

namespace WhereWeFishin.Tests.Controllers;

public class FishingSpotsControllerTests
{
    private readonly IRepository<FishingSpot> _spotRepository;
    private readonly IRepository<FishingSession> _sessionRepository;
    private readonly IRepository<Review> _reviewRepository;
    private readonly IRepository<Pontoon> _pontoonRepository;
    private readonly IRepository<SpotEmployee> _employeeRepository;
    private readonly IRepository<FishStocking> _stockingRepository;
    private readonly IOutputCacheStore _cacheStore;
    private readonly FishingSpotsController _controller;

    public FishingSpotsControllerTests()
    {
        _spotRepository = Substitute.For<IRepository<FishingSpot>>();
        _sessionRepository = Substitute.For<IRepository<FishingSession>>();
        _reviewRepository = Substitute.For<IRepository<Review>>();
        _pontoonRepository = Substitute.For<IRepository<Pontoon>>();
        _employeeRepository = Substitute.For<IRepository<SpotEmployee>>();
        _stockingRepository = Substitute.For<IRepository<FishStocking>>();
        _cacheStore = Substitute.For<IOutputCacheStore>();
        _controller = new FishingSpotsController(
            _spotRepository, _sessionRepository, _reviewRepository,
            _pontoonRepository, _employeeRepository, _stockingRepository, _cacheStore);
    }

    private void SetUser(int userId, string role = Roles.User)
    {
        ControllerContextFactory.SetAuthenticatedUser(_controller, userId, role);
    }

    private static FishingSpot CreateSpot(int id = 1, string name = "Lake Spot") => new()
    {
        Id = id,
        Name = name,
        Description = "Nice place",
        Latitude = 45.0,
        Longitude = 25.0,
        PricePerHour = 10m,
        UserId = 1
    };

    private static FishingSession CreateSession(int id, SessionStatus status, decimal totalPrice) => new()
    {
        Id = id,
        UserId = 10,
        FishingSpotId = 1,
        StartDate = DateTime.UtcNow.AddDays(1),
        DurationHours = 12,
        Status = status,
        TotalPrice = totalPrice
    };

    private static Review CreateReview(int id, int rating) => new()
    {
        Id = id,
        FishingSpotId = 1,
        UserId = id,
        Rating = rating
    };


    [Fact]
    public async Task GetFishingSpots_ReturnsAllSpots()
    {
        // Arrange
        var spots = new List<FishingSpot> { CreateSpot(1, "River"), CreateSpot(2, "Lake") };
        _spotRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(spots);

        // Act
        var result = await _controller.GetFishingSpots();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedSpots = Assert.IsAssignableFrom<IEnumerable<FishingSpotDto>>(okResult.Value);
        Assert.Equal(2, returnedSpots.Count());
    }

    [Fact]
    public async Task GetFishingSpots_WhenEmpty_ReturnsEmptyList()
    {
        // Arrange
        _spotRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<FishingSpot>());

        // Act
        var result = await _controller.GetFishingSpots();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedSpots = Assert.IsAssignableFrom<IEnumerable<FishingSpotDto>>(okResult.Value);
        Assert.Empty(returnedSpots);
    }

    [Fact]
    public async Task GetManagedFishingSpots_AsManager_ReturnsOnlyManagedSpots()
    {
        // Arrange
        ControllerContextFactory.SetAuthenticatedUser(_controller, 7, Roles.Manager);
        var managerSpot = CreateSpot(1, "North Lake");
        managerSpot.ManagerId = 7;
        var ownerSpot = CreateSpot(2, "South Lake");
        ownerSpot.UserId = 7;
        var spots = new List<FishingSpot>
        {
            managerSpot,
            ownerSpot,
        };
        _spotRepository
            .FindAsync(Arg.Any<System.Linq.Expressions.Expression<Func<FishingSpot, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(spots);

        // Act
        var result = await _controller.GetManagedFishingSpots();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedSpots = Assert.IsAssignableFrom<IEnumerable<FishingSpotDto>>(okResult.Value).ToList();
        Assert.Equal(2, returnedSpots.Count);
        await _spotRepository.Received(1)
            .FindAsync(Arg.Any<System.Linq.Expressions.Expression<Func<FishingSpot, bool>>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetManagedFishingSpots_AsAdmin_ReturnsAllSpots()
    {
        // Arrange
        ControllerContextFactory.SetAuthenticatedUser(_controller, 3, Roles.Admin);
        var spots = new List<FishingSpot> { CreateSpot(1, "River"), CreateSpot(2, "Lake") };
        _spotRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(spots);

        // Act
        var result = await _controller.GetManagedFishingSpots();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedSpots = Assert.IsAssignableFrom<IEnumerable<FishingSpotDto>>(okResult.Value);
        Assert.Equal(2, returnedSpots.Count());
        await _spotRepository.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }


    [Fact]
    public async Task GetFishingSpot_WithValidId_ReturnsSpot()
    {
        // Arrange
        var spot = CreateSpot(1, "River Bank");
        _spotRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(spot);

        // Act
        var result = await _controller.GetFishingSpot(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedSpot = Assert.IsType<FishingSpotDto>(okResult.Value);
        Assert.Equal("River Bank", returnedSpot.Name);
        Assert.Equal(45.0, returnedSpot.Latitude);
        Assert.Equal(10m, returnedSpot.PricePerHour);
    }

    [Fact]
    public async Task GetFishingSpot_WithManager_ReturnsManagerDisplayName()
    {
        // Arrange
        var spot = CreateSpot(1, "Managed Spot");
        spot.Manager = new User
        {
            Id = 15,
            Username = "manager15",
            FirstName = "Mihai",
            LastName = "Ionescu"
        };
        spot.ManagerId = 15;
        _spotRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(spot);

        // Act
        var result = await _controller.GetFishingSpot(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedSpot = Assert.IsType<FishingSpotDto>(okResult.Value);
        Assert.Equal("Mihai Ionescu", returnedSpot.ManagerName);
        Assert.Equal(15, returnedSpot.ManagerId);
    }

    [Fact]
    public async Task GetFishingSpot_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        _spotRepository.GetByIdAsync(999, Arg.Any<CancellationToken>()).Returns((FishingSpot?)null);

        // Act
        var result = await _controller.GetFishingSpot(999);

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }


    [Fact]
    public async Task CreateFishingSpot_WithValidData_ReturnsCreated()
    {
        // Arrange
        SetUser(1, Roles.Admin);
        var createDto = new CreateFishingSpotDto
        {
            Name = "New Spot",
            Description = "A great location",
            Latitude = 46.5,
            Longitude = 26.5,
            PricePerHour = 15m
        };
        _spotRepository.AddAsync(Arg.Any<FishingSpot>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<FishingSpot>());

        // Act
        var result = await _controller.CreateFishingSpot(createDto);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var returnedSpot = Assert.IsType<FishingSpotDto>(createdResult.Value);
        Assert.Equal("New Spot", returnedSpot.Name);
        Assert.Equal(46.5, returnedSpot.Latitude);
        Assert.Equal(15m, returnedSpot.PricePerHour);
    }

    [Fact]
    public async Task CreateFishingSpot_CallsAddAsync()
    {
        // Arrange
        SetUser(1, Roles.Admin);
        var createDto = new CreateFishingSpotDto { Name = "Spot", Latitude = 1, Longitude = 1 };
        _spotRepository.AddAsync(Arg.Any<FishingSpot>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<FishingSpot>());

        // Act
        await _controller.CreateFishingSpot(createDto);

        // Assert
        await _spotRepository.Received(1).AddAsync(Arg.Any<FishingSpot>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateFishingSpot_WhenAnonymous_ReturnsUnauthorized()
    {
        // Arrange
        ControllerContextFactory.SetAnonymousUser(_controller);

        // Act
        var result = await _controller.CreateFishingSpot(new CreateFishingSpotDto
        {
            Name = "Spot",
            Latitude = 1,
            Longitude = 1
        });

        // Assert
        Assert.IsType<UnauthorizedResult>(result.Result);
        await _spotRepository.DidNotReceive().AddAsync(Arg.Any<FishingSpot>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateFishingSpot_EvictsCacheAndMapsManager()
    {
        // Arrange
        SetUser(7, Roles.Admin);
        _spotRepository.AddAsync(Arg.Any<FishingSpot>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<FishingSpot>());

        var dto = new CreateFishingSpotDto
        {
            Name = "Managed Spot",
            Latitude = 44.5,
            Longitude = 26.1,
            ManagerId = 3,
            PricePerHour = 25m
        };

        // Act
        var result = await _controller.CreateFishingSpot(dto);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var returnedSpot = Assert.IsType<FishingSpotDto>(createdResult.Value);
        Assert.Equal(7, returnedSpot.UserId);
        Assert.Equal(3, returnedSpot.ManagerId);
        await _cacheStore.Received(1).EvictByTagAsync("fishingspots", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateFishingSpot_WhenUserIsNotAdmin_ReturnsForbid()
    {
        // Arrange
        SetUser(7, Roles.Manager);

        // Act
        var result = await _controller.CreateFishingSpot(new CreateFishingSpotDto
        {
            Name = "Blocked Spot",
            Latitude = 44.1,
            Longitude = 26.1
        });

        // Assert
        Assert.IsType<ForbidResult>(result.Result);
        await _spotRepository.DidNotReceive().AddAsync(Arg.Any<FishingSpot>(), Arg.Any<CancellationToken>());
    }


    [Fact]
    public async Task UpdateFishingSpot_WithValidData_ReturnsNoContent()
    {
        // Arrange
        SetUser(1);
        var spot = CreateSpot(1, "Old Name");
        var updateDto = new UpdateFishingSpotDto { Name = "Updated Name", PricePerHour = 20m };
        _spotRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(spot);

        // Act
        var result = await _controller.UpdateFishingSpot(1, updateDto);

        // Assert
        Assert.IsType<NoContentResult>(result);
        await _spotRepository.Received(1).UpdateAsync(Arg.Any<FishingSpot>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateFishingSpot_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        _spotRepository.GetByIdAsync(999, Arg.Any<CancellationToken>()).Returns((FishingSpot?)null);
        var updateDto = new UpdateFishingSpotDto { Name = "Updated" };

        // Act
        var result = await _controller.UpdateFishingSpot(999, updateDto);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateFishingSpot_WhenUserCannotManageSpot_ReturnsForbid()
    {
        // Arrange
        SetUser(99);
        _spotRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(CreateSpot(1));

        // Act
        var result = await _controller.UpdateFishingSpot(1, new UpdateFishingSpotDto { Name = "Blocked" });

        // Assert
        Assert.IsType<ForbidResult>(result);
        await _spotRepository.DidNotReceive().UpdateAsync(Arg.Any<FishingSpot>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateFishingSpot_UpdatesOnlyProvidedFields()
    {
        // Arrange
        SetUser(1);
        var spot = CreateSpot(1, "Original Name");
        spot.Description = "Original Description";
        var updateDto = new UpdateFishingSpotDto { Name = "New Name" };
        _spotRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(spot);

        // Act
        await _controller.UpdateFishingSpot(1, updateDto);

        // Assert – description should remain unchanged
        await _spotRepository.Received(1).UpdateAsync(
            Arg.Is<FishingSpot>(s => s.Name == "New Name" && s.Description == "Original Description"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateFishingSpot_ClearManagerAndResetMapView_UpdatesSpecialFields()
    {
        // Arrange
        SetUser(1);
        var spot = CreateSpot(1, "Original Name");
        spot.ManagerId = 8;
        spot.DefaultZoom = 12;
        spot.DefaultCenterLat = 45.7;
        spot.DefaultCenterLng = 25.7;
        spot.FishSpecies = "Pike";
        _spotRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(spot);

        var updateDto = new UpdateFishingSpotDto
        {
            ClearManager = true,
            ResetDefaultMapView = true,
            FishSpecies = "Carp, Catfish"
        };

        // Act
        var result = await _controller.UpdateFishingSpot(1, updateDto);

        // Assert
        Assert.IsType<NoContentResult>(result);
        await _spotRepository.Received(1).UpdateAsync(
            Arg.Is<FishingSpot>(current =>
                current.ManagerId == null &&
                current.DefaultZoom == null &&
                current.DefaultCenterLat == null &&
                current.DefaultCenterLng == null &&
                current.FishSpecies == "Carp, Catfish"),
            Arg.Any<CancellationToken>());
        await _cacheStore.Received(1).EvictByTagAsync("fishingspots", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateFishingSpot_SetsMapViewValuesWithoutResettingThem()
    {
        // Arrange
        SetUser(1);
        var spot = CreateSpot(1, "Map Spot");
        _spotRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(spot);

        var updateDto = new UpdateFishingSpotDto
        {
            DefaultZoom = 14,
            DefaultCenterLat = 46.11,
            DefaultCenterLng = 24.22
        };

        // Act
        await _controller.UpdateFishingSpot(1, updateDto);

        // Assert
        await _spotRepository.Received(1).UpdateAsync(
            Arg.Is<FishingSpot>(current =>
                current.DefaultZoom == 14 &&
                current.DefaultCenterLat == 46.11 &&
                current.DefaultCenterLng == 24.22),
            Arg.Any<CancellationToken>());
    }


    [Fact]
    public async Task DeleteFishingSpot_WithValidId_ReturnsNoContent()
    {
        // Arrange
        SetUser(1);
        _spotRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(CreateSpot(1));

        // Act
        var result = await _controller.DeleteFishingSpot(1);

        // Assert
        Assert.IsType<NoContentResult>(result);
        await _spotRepository.Received(1).DeleteAsync(1, Arg.Any<CancellationToken>());
        await _cacheStore.Received(1).EvictByTagAsync("fishingspots", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteFishingSpot_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        _spotRepository.GetByIdAsync(999, Arg.Any<CancellationToken>()).Returns((FishingSpot?)null);

        // Act
        var result = await _controller.DeleteFishingSpot(999);

        // Assert
        Assert.IsType<NotFoundResult>(result);
        await _spotRepository.DidNotReceive().DeleteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteFishingSpot_WhenUserCannotManageSpot_ReturnsForbid()
    {
        // Arrange
        SetUser(55);
        _spotRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(CreateSpot(1));

        // Act
        var result = await _controller.DeleteFishingSpot(1);

        // Assert
        Assert.IsType<ForbidResult>(result);
        await _spotRepository.DidNotReceive().DeleteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetSpotStatistics_WithValidData_ReturnsAggregatedStatistics()
    {
        // Arrange
        SetUser(1);
        _spotRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(CreateSpot(1));
        _sessionRepository.FindAsync(Arg.Any<System.Linq.Expressions.Expression<Func<FishingSession, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                CreateSession(1, SessionStatus.Confirmed, 120m),
                CreateSession(2, SessionStatus.Pending, 80m),
                CreateSession(3, SessionStatus.Cancelled, 40m)
            });
        _reviewRepository.FindAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Review, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                CreateReview(1, 5),
                CreateReview(2, 3)
            });
        _pontoonRepository.CountAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Pontoon, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(4);
        _employeeRepository.CountAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SpotEmployee, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(2);
        _stockingRepository.CountAsync(Arg.Any<System.Linq.Expressions.Expression<Func<FishStocking, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(7);

        // Act
        var result = await _controller.GetSpotStatistics(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var statistics = Assert.IsType<SpotStatisticsDto>(okResult.Value);
        Assert.Equal(3, statistics.TotalBookings);
        Assert.Equal(2, statistics.ActiveBookings);
        Assert.Equal(1, statistics.CancelledBookings);
        Assert.Equal(200m, statistics.TotalRevenue);
        Assert.Equal(2, statistics.TotalReviews);
        Assert.Equal(4d, statistics.AverageRating);
        Assert.Equal(4, statistics.TotalPontoons);
        Assert.Equal(2, statistics.TotalEmployees);
        Assert.Equal(7, statistics.TotalStockings);
    }

    [Fact]
    public async Task GetSpotStatistics_WithNoReviews_ReturnsNullAverage()
    {
        // Arrange
        SetUser(1);
        _spotRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(CreateSpot(1));
        _sessionRepository.FindAsync(Arg.Any<System.Linq.Expressions.Expression<Func<FishingSession, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<FishingSession>());
        _reviewRepository.FindAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Review, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Review>());

        // Act
        var result = await _controller.GetSpotStatistics(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var statistics = Assert.IsType<SpotStatisticsDto>(okResult.Value);
        Assert.Null(statistics.AverageRating);
    }

    [Fact]
    public async Task GetSpotStatistics_WhenSpotMissing_ReturnsNotFound()
    {
        // Arrange
        _spotRepository.GetByIdAsync(999, Arg.Any<CancellationToken>()).Returns((FishingSpot?)null);

        // Act
        var result = await _controller.GetSpotStatistics(999);

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetSpotStatistics_WhenUserCannotManageSpot_ReturnsForbid()
    {
        // Arrange
        SetUser(999);
        _spotRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(CreateSpot(1));

        // Act
        var result = await _controller.GetSpotStatistics(1);

        // Assert
        Assert.IsType<ForbidResult>(result.Result);
    }
}
