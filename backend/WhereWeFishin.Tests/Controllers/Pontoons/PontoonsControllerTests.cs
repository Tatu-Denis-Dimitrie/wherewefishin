using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhereWeFishin.API.Controllers;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Interfaces;
using WhereWeFishin.Database.Context;
using WhereWeFishin.Database.Repositories;
using WhereWeFishin.Tests.TestHelpers;

namespace WhereWeFishin.Tests.Controllers;

public class PontoonsControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly PontoonRepository _pontoonRepository;
    private readonly IRepository<FishingSpot> _spotRepository;
    private readonly PontoonsController _controller;

    public PontoonsControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _pontoonRepository = new PontoonRepository(_context);
        _spotRepository = new Repository<FishingSpot>(_context);
        _controller = new PontoonsController(_pontoonRepository, _spotRepository);

        SetUser(1);
    }

    private void SetUser(int userId)
    {
        ControllerContextFactory.SetAuthenticatedUser(_controller, userId);
    }

    private FishingSpot AddSpot(int id = 1, int userId = 1)
    {
        var spot = new FishingSpot
        {
            Id = id,
            Name = $"Spot {id}",
            Latitude = 45,
            Longitude = 25,
            PricePerHour = 10,
            UserId = userId
        };
        _context.FishingSpots.Add(spot);
        _context.SaveChanges();
        return spot;
    }

    private Pontoon AddPontoon(int id, int spotId, string name)
    {
        var pontoon = new Pontoon
        {
            Id = id,
            FishingSpotId = spotId,
            Name = name,
            SouthWestLat = 44.9,
            SouthWestLng = 24.9,
            NorthEastLat = 45.1,
            NorthEastLng = 25.1,
            Color = "#3388ff"
        };
        _context.Pontoons.Add(pontoon);
        _context.SaveChanges();
        return pontoon;
    }

    [Fact]
    public async Task GetSpotPontoons_WhenSpotMissing_ReturnsNotFound()
    {
        var result = await _controller.GetSpotPontoons(999);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetSpotPontoons_ReturnsPontoonsOrderedByName()
    {
        AddSpot(1);
        AddPontoon(1, 1, "Bravo");
        AddPontoon(2, 1, "Alpha");

        var result = await _controller.GetSpotPontoons(1);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var pontoons = Assert.IsAssignableFrom<IEnumerable<PontoonDto>>(okResult.Value).ToList();
        Assert.Equal(2, pontoons.Count);
        Assert.Equal("Alpha", pontoons[0].Name);
        Assert.Equal("Bravo", pontoons[1].Name);
    }

    [Fact]
    public async Task GetPontoon_WhenMissing_ReturnsNotFound()
    {
        var result = await _controller.GetPontoon(404);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetPontoon_WhenFound_ReturnsMappedPontoon()
    {
        AddSpot(1);
        AddPontoon(1, 1, "Alpha");

        var result = await _controller.GetPontoon(1);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<PontoonDto>(okResult.Value);
        Assert.Equal("Alpha", dto.Name);
        Assert.Equal(1, dto.FishingSpotId);
    }

    [Fact]
    public async Task CreatePontoon_WhenSpotMissing_ReturnsNotFound()
    {
        var result = await _controller.CreatePontoon(new CreatePontoonDto
        {
            FishingSpotId = 999,
            Name = "Alpha"
        });

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreatePontoon_WhenUserCannotManageSpot_ReturnsForbid()
    {
        AddSpot(1, userId: 10);
        SetUser(99);

        var result = await _controller.CreatePontoon(new CreatePontoonDto
        {
            FishingSpotId = 1,
            Name = "Alpha"
        });

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task CreatePontoon_WithValidData_ReturnsCreatedAndAppliesDefaultColor()
    {
        AddSpot(1, userId: 1);

        var result = await _controller.CreatePontoon(new CreatePontoonDto
        {
            FishingSpotId = 1,
            Name = "Alpha",
            SouthWestLat = 44.9,
            SouthWestLng = 24.9,
            NorthEastLat = 45.1,
            NorthEastLng = 25.1,
            Coordinates = "coords"
        });

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<PontoonDto>(createdResult.Value);
        Assert.True(dto.Id > 0);
        Assert.Equal("#3388ff", dto.Color);
        Assert.Equal("coords", dto.Coordinates);
    }

    [Fact]
    public async Task UpdatePontoon_WhenPontoonMissing_ReturnsNotFound()
    {
        var result = await _controller.UpdatePontoon(999, new UpdatePontoonDto { Name = "Updated" });

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetPontoon_WhenOwningSpotIsDeleted_ReturnsNotFound()
    {
        var spot = AddSpot(1, userId: 1);
        AddPontoon(1, 1, "Alpha");
        spot.IsDeleted = true;
        _context.SaveChanges();

        var result = await _controller.GetPontoon(1);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UpdatePontoon_WhenOwningSpotIsDeleted_ReturnsNotFound()
    {
        var spot = AddSpot(1, userId: 1);
        AddPontoon(1, 1, "Alpha");
        spot.IsDeleted = true;
        _context.SaveChanges();

        var result = await _controller.UpdatePontoon(1, new UpdatePontoonDto { Name = "Updated" });

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdatePontoon_WhenUserCannotManageSpot_ReturnsForbid()
    {
        AddSpot(1, userId: 10);
        AddPontoon(1, 1, "Alpha");
        SetUser(99);

        var result = await _controller.UpdatePontoon(1, new UpdatePontoonDto { Name = "Updated" });

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task UpdatePontoon_WithValidData_UpdatesFields()
    {
        AddSpot(1, userId: 1);
        AddPontoon(1, 1, "Alpha");

        var result = await _controller.UpdatePontoon(1, new UpdatePontoonDto
        {
            Name = "Updated",
            Color = "#ffffff",
            Coordinates = "new"
        });

        Assert.IsType<NoContentResult>(result);
        var pontoon = await _pontoonRepository.GetByIdAsync(1);
        Assert.Equal("Updated", pontoon!.Name);
        Assert.Equal("#ffffff", pontoon.Color);
        Assert.Equal("new", pontoon.Coordinates);
    }

    [Fact]
    public async Task DeletePontoon_WhenMissing_ReturnsNotFound()
    {
        var result = await _controller.DeletePontoon(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeletePontoon_WhenOwningSpotIsDeleted_ReturnsNotFound()
    {
        var spot = AddSpot(1, userId: 1);
        AddPontoon(1, 1, "Alpha");
        spot.IsDeleted = true;
        _context.SaveChanges();

        var result = await _controller.DeletePontoon(1);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeletePontoon_WhenUserCannotManageSpot_ReturnsForbid()
    {
        AddSpot(1, userId: 10);
        AddPontoon(1, 1, "Alpha");
        SetUser(99);

        var result = await _controller.DeletePontoon(1);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task DeletePontoon_WithValidData_SoftDeletesPontoon()
    {
        AddSpot(1, userId: 1);
        AddPontoon(1, 1, "Alpha");

        var result = await _controller.DeletePontoon(1);

        Assert.IsType<NoContentResult>(result);
        var pontoon = await _context.Pontoons.IgnoreQueryFilters().SingleAsync(current => current.Id == 1);
        Assert.True(pontoon.IsDeleted);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}