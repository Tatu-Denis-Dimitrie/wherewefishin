using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using WhereWeFishin.API.Controllers;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Enums;
using WhereWeFishin.Core.Interfaces;
using WhereWeFishin.Database.Context;
using WhereWeFishin.Database.Repositories;
using WhereWeFishin.Tests.TestHelpers;

namespace WhereWeFishin.Tests.Controllers;

public class AdminControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<VideoAnalysis> _videoRepository;
    private readonly IRepository<FishingSpot> _spotRepository;
    private readonly IRepository<FishingSession> _sessionRepository;
    private readonly IOutputCacheStore _cacheStore;
    private readonly AdminController _controller;

    public AdminControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _userRepository = new Repository<User>(_context);
        _videoRepository = new Repository<VideoAnalysis>(_context);
        _spotRepository = new FishingSpotRepository(_context);
        _sessionRepository = new Repository<FishingSession>(_context);
        _cacheStore = Substitute.For<IOutputCacheStore>();

        _controller = new AdminController(
            _userRepository,
            _videoRepository,
            _spotRepository,
            _sessionRepository,
            _cacheStore,
            _context);

        ControllerContextFactory.SetAuthenticatedUser(_controller, 99, Roles.Admin);
    }

    private User AddUser(int id, UserRole role, bool isDeleted = false)
    {
        var user = new User
        {
            Id = id,
            Username = $"user{id}",
            Email = $"user{id}@test.com",
            PasswordHash = "hash",
            Role = role,
            IsDeleted = isDeleted,
            FirstName = $"First{id}",
            LastName = $"Last{id}"
        };
        _context.Users.Add(user);
        _context.SaveChanges();
        return user;
    }

    private FishingSpot AddSpot(int id, int userId, int? managerId = null, bool isDeleted = false)
    {
        var spot = new FishingSpot
        {
            Id = id,
            Name = $"Spot {id}",
            Latitude = 45,
            Longitude = 25,
            PricePerHour = 10,
            UserId = userId,
            ManagerId = managerId,
            IsDeleted = isDeleted
        };
        _context.FishingSpots.Add(spot);
        _context.SaveChanges();
        return spot;
    }

    private void AddPontoon(int id, int spotId)
    {
        _context.Pontoons.Add(new Pontoon
        {
            Id = id,
            FishingSpotId = spotId,
            Name = $"Pontoon {id}",
            SouthWestLat = 44.9,
            SouthWestLng = 24.9,
            NorthEastLat = 45.1,
            NorthEastLng = 25.1
        });
        _context.SaveChanges();
    }

    private void AddReview(int id, int spotId, int userId)
    {
        _context.Reviews.Add(new Review
        {
            Id = id,
            FishingSpotId = spotId,
            UserId = userId,
            Rating = 5
        });
        _context.SaveChanges();
    }

    private void AddVideoAnalysis(int id, int userId, AnalysisStatus status, bool isDeleted = false)
    {
        _context.VideoAnalyses.Add(new VideoAnalysis
        {
            Id = id,
            UserId = userId,
            FileName = $"video-{id}.mp4",
            VideoUrl = $"uploads/video-{id}.mp4",
            Status = status,
            AnalyzedAt = DateTime.UtcNow,
            IsDeleted = isDeleted
        });
        _context.SaveChanges();
    }

    private void AddSession(int id, int userId, SessionStatus status, bool isDeleted = false)
    {
        _context.FishingSessions.Add(new FishingSession
        {
            Id = id,
            UserId = userId,
            FishingSpotId = 1,
            StartDate = DateTime.UtcNow,
            DurationHours = 12,
            TotalPrice = 100,
            Status = status,
            IsDeleted = isDeleted
        });
        _context.SaveChanges();
    }

    private static T GetAnonymousProperty<T>(object instance, string propertyName)
        => (T)instance.GetType().GetProperty(propertyName)!.GetValue(instance)!;

    [Fact]
    public async Task GetStats_ReturnsAggregatedCounts()
    {
        AddUser(1, UserRole.User);
        AddUser(2, UserRole.Manager);
        AddUser(3, UserRole.Admin);
        AddUser(4, UserRole.User, isDeleted: true);
        AddSpot(1, 1);
        AddSpot(2, 2);
        AddPontoon(1, 1);
        AddReview(1, 1, 1);
        AddVideoAnalysis(1, 1, AnalysisStatus.Completed);
        AddVideoAnalysis(2, 1, AnalysisStatus.Failed);
        AddVideoAnalysis(3, 1, AnalysisStatus.Completed, isDeleted: true);
        AddSession(1, 1, SessionStatus.Confirmed);
        AddSession(2, 1, SessionStatus.Cancelled);
        AddSession(3, 1, SessionStatus.Confirmed, isDeleted: true);

        var result = await _controller.GetStats();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(3, GetAnonymousProperty<int>(okResult.Value!, "totalUsers"));
        Assert.Equal(1, GetAnonymousProperty<int>(okResult.Value!, "totalManagers"));
        Assert.Equal(1, GetAnonymousProperty<int>(okResult.Value!, "totalAdmins"));
        Assert.Equal(1, GetAnonymousProperty<int>(okResult.Value!, "deactivatedUsers"));
        Assert.Equal(2, GetAnonymousProperty<int>(okResult.Value!, "totalAnalyses"));
        Assert.Equal(1, GetAnonymousProperty<int>(okResult.Value!, "completedAnalyses"));
        Assert.Equal(1, GetAnonymousProperty<int>(okResult.Value!, "failedAnalyses"));
        Assert.Equal(2, GetAnonymousProperty<int>(okResult.Value!, "totalBookings"));
        Assert.Equal(1, GetAnonymousProperty<int>(okResult.Value!, "confirmedBookings"));
        Assert.Equal(1, GetAnonymousProperty<int>(okResult.Value!, "cancelledBookings"));
        Assert.Equal(2, GetAnonymousProperty<int>(okResult.Value!, "totalSpots"));
        Assert.Equal(1, GetAnonymousProperty<int>(okResult.Value!, "totalPontoons"));
        Assert.Equal(1, GetAnonymousProperty<int>(okResult.Value!, "totalReviews"));
    }

    [Fact]
    public async Task GetAllUsers_ReturnsIncludingDeletedUsers()
    {
        AddUser(1, UserRole.User);
        AddUser(2, UserRole.Employee, isDeleted: true);

        var result = await _controller.GetAllUsers();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<PagedResponseDto<UserDto>>(okResult.Value);
        Assert.Equal(2, payload.TotalItems);
        Assert.Equal(1, payload.Page);
        Assert.Equal(10, payload.PageSize);
        Assert.Equal(2, payload.Items.Count);
        Assert.True(payload.Items[0].IsActive);
        Assert.False(payload.Items[1].IsActive);
    }

    [Fact]
    public async Task GetAllUsers_AppliesRequestedPagination()
    {
        for (var index = 1; index <= 12; index++)
        {
            AddUser(index, UserRole.User);
        }

        var result = await _controller.GetAllUsers(page: 2, pageSize: 5);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<PagedResponseDto<UserDto>>(okResult.Value);
        Assert.Equal(2, payload.Page);
        Assert.Equal(5, payload.PageSize);
        Assert.Equal(12, payload.TotalItems);
        Assert.Equal([6, 7, 8, 9, 10], payload.Items.Select(user => user.Id).ToArray());
    }

    [Fact]
    public async Task ToggleUserStatus_WhenMissing_ReturnsNotFound()
    {
        var result = await _controller.ToggleUserStatus(999, new ToggleStatusDto { Enable = true });

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ToggleUserStatus_WhenUserIsAdmin_ReturnsBadRequest()
    {
        AddUser(1, UserRole.Admin);

        var result = await _controller.ToggleUserStatus(1, new ToggleStatusDto { Enable = false });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ToggleUserStatus_UpdatesDeletedFlag()
    {
        AddUser(1, UserRole.User);

        var result = await _controller.ToggleUserStatus(1, new ToggleStatusDto { Enable = false });

        Assert.IsType<OkObjectResult>(result);
        var user = await _userRepository.GetByIdIncludingDeletedAsync(1);
        Assert.True(user!.IsDeleted);
    }

    [Fact]
    public async Task UpdateUserRole_WhenMissing_ReturnsNotFound()
    {
        var result = await _controller.UpdateUserRole(999, new UpdateRoleDto { Role = Roles.Manager });

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateUserRole_WhenRoleIsInvalid_ReturnsBadRequest()
    {
        AddUser(1, UserRole.User);

        var result = await _controller.UpdateUserRole(1, new UpdateRoleDto { Role = "Nope" });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UpdateUserRole_WithValidRole_UpdatesUser()
    {
        AddUser(1, UserRole.User);

        var result = await _controller.UpdateUserRole(1, new UpdateRoleDto { Role = Roles.Manager });

        Assert.IsType<OkObjectResult>(result);
        var user = await _userRepository.GetByIdAsync(1);
        Assert.Equal(UserRole.Manager, user!.Role);
    }

    [Fact]
    public async Task DeleteUser_WhenMissing_ReturnsNotFound()
    {
        var result = await _controller.DeleteUser(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeleteUser_WhenUserIsAdmin_ReturnsBadRequest()
    {
        AddUser(1, UserRole.Admin);

        var result = await _controller.DeleteUser(1);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DeleteUser_WhenUserHasNoData_HardDeletesUser()
    {
        AddUser(1, UserRole.User);

        var result = await _controller.DeleteUser(1);

        Assert.IsType<NoContentResult>(result);
        Assert.Null(await _context.Users.IgnoreQueryFilters().SingleOrDefaultAsync(user => user.Id == 1));
    }

    [Fact]
    public async Task DeleteUser_WhenUserHasRelatedData_SoftDeletesUser()
    {
        AddUser(1, UserRole.User);
        AddSpot(1, 1);
        AddSession(1, 1, SessionStatus.Confirmed);

        var result = await _controller.DeleteUser(1);

        Assert.IsType<NoContentResult>(result);
        var user = await _userRepository.GetByIdIncludingDeletedAsync(1);
        Assert.True(user!.IsDeleted);
    }

    [Fact]
    public async Task GetAllFishingSpots_ReturnsMappedSpotsWithManagerName()
    {
        AddUser(1, UserRole.User);
        AddUser(2, UserRole.Manager);
        AddSpot(1, 1, managerId: 2);

        var result = await _controller.GetAllFishingSpots();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<PagedResponseDto<FishingSpotDto>>(okResult.Value);
        Assert.Single(payload.Items);
        Assert.Equal("First2 Last2", payload.Items[0].ManagerName);
    }

    [Fact]
    public async Task GetAllFishingSpots_AppliesRequestedPagination()
    {
        AddUser(1, UserRole.User);
        for (var index = 1; index <= 12; index++)
        {
            AddSpot(index, 1);
        }

        var result = await _controller.GetAllFishingSpots(page: 2, pageSize: 5);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<PagedResponseDto<FishingSpotDto>>(okResult.Value);
        Assert.Equal(2, payload.Page);
        Assert.Equal(5, payload.PageSize);
        Assert.Equal(12, payload.TotalItems);
        Assert.Equal(5, payload.Items.Count);
    }

    [Fact]
    public async Task UpdateFishingSpot_WhenMissing_ReturnsNotFound()
    {
        var result = await _controller.UpdateFishingSpot(999, new UpdateFishingSpotDto { Name = "Updated" });

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateFishingSpot_WithValidData_UpdatesSpotAndEvictsCache()
    {
        AddSpot(1, 1, managerId: 2);

        var result = await _controller.UpdateFishingSpot(1, new UpdateFishingSpotDto
        {
            Name = "Updated",
            ClearManager = true,
            ResetDefaultMapView = true,
            FishSpecies = "Carp"
        });

        Assert.IsType<OkObjectResult>(result);
        var spot = await _spotRepository.GetByIdAsync(1);
        Assert.Equal("Updated", spot!.Name);
        Assert.Null(spot.ManagerId);
        Assert.Equal("Carp", spot.FishSpecies);
        await _cacheStore.Received(1).EvictByTagAsync("fishingspots", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteFishingSpot_WhenMissing_ReturnsNotFound()
    {
        var result = await _controller.DeleteFishingSpot(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeleteFishingSpot_WithValidData_SoftDeletesSpotAndEvictsCache()
    {
        AddSpot(1, 1);

        var result = await _controller.DeleteFishingSpot(1);

        Assert.IsType<NoContentResult>(result);
        var spot = await _context.FishingSpots.IgnoreQueryFilters().SingleAsync(current => current.Id == 1);
        Assert.True(spot.IsDeleted);
        await _cacheStore.Received(1).EvictByTagAsync("fishingspots", Arg.Any<CancellationToken>());
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}