using Microsoft.EntityFrameworkCore;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Enums;
using WhereWeFishin.Database.Context;
using WhereWeFishin.Database.Repositories;

namespace WhereWeFishin.Tests.Repositories;

public class FishingSpotRepositoryTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly FishingSpotRepository _repository;

    public FishingSpotRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _repository = new FishingSpotRepository(_context);
    }

    [Fact]
    public async Task GetByIdAsync_WhenSpotExists_ReturnsSpotWithManagerIncluded()
    {
        var owner = AddUser(1, UserRole.User, "owner");
        var manager = AddUser(2, UserRole.Manager, "manager");
        AddSpot(1, owner, manager);

        var result = await _repository.GetByIdAsync(1);

        Assert.NotNull(result);
        Assert.Equal("Spot 1", result!.Name);
        Assert.NotNull(result.Manager);
        Assert.Equal(manager.Id, result.Manager!.Id);
        Assert.Equal(manager.FirstName, result.Manager.FirstName);
    }

    [Fact]
    public async Task GetByIdAsync_WhenSpotIsSoftDeleted_ReturnsNull()
    {
        var owner = AddUser(1, UserRole.User, "owner");
        AddSpot(1, owner, isDeleted: true);

        var result = await _repository.GetByIdAsync(1);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyActiveSpotsAndIncludesManagers()
    {
        var owner = AddUser(1, UserRole.User, "owner");
        var manager = AddUser(2, UserRole.Manager, "manager");
        AddSpot(1, owner, manager);
        AddSpot(2, owner, isDeleted: true);

        var result = (await _repository.GetAllAsync()).ToList();

        var spot = Assert.Single(result);
        Assert.Equal(1, spot.Id);
        Assert.NotNull(spot.Manager);
        Assert.Equal(manager.Id, spot.Manager!.Id);
    }

    private User AddUser(int id, UserRole role, string prefix)
    {
        var user = new User
        {
            Id = id,
            Username = $"{prefix}{id}",
            Email = $"{prefix}{id}@test.com",
            PasswordHash = "hash",
            Role = role,
            FirstName = $"First{id}",
            LastName = $"Last{id}"
        };

        _context.Users.Add(user);
        _context.SaveChanges();
        return user;
    }

    private void AddSpot(int id, User owner, User? manager = null, bool isDeleted = false)
    {
        _context.FishingSpots.Add(new FishingSpot
        {
            Id = id,
            Name = $"Spot {id}",
            Latitude = 45,
            Longitude = 25,
            PricePerHour = 10,
            UserId = owner.Id,
            User = owner,
            ManagerId = manager?.Id,
            Manager = manager,
            IsDeleted = isDeleted
        });
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}