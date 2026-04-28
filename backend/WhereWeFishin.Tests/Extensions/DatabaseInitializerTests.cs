using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WhereWeFishin.API.Extensions;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Enums;
using WhereWeFishin.Database.Context;

namespace WhereWeFishin.Tests.Extensions;

public class DatabaseInitializerTests : IDisposable
{
    private readonly ApplicationDbContext _context;

    public DatabaseInitializerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
    }

    [Fact]
    public async Task SeedMissingDataAsync_WhenDatabaseAlreadyHasData_AddsMissingSeedRecordsWithoutRemovingExistingOnes()
    {
        var existingSeededAdmin = new User
        {
            Username = "admin",
            Email = "admin@wherewefishin.com",
            PasswordHash = "hash",
            Role = UserRole.Admin
        };
        var customUser = new User
        {
            Username = "local_owner",
            Email = "local_owner@test.com",
            PasswordHash = "hash",
            Role = UserRole.User
        };

        _context.Users.AddRange(existingSeededAdmin, customUser);
        await _context.SaveChangesAsync();

        _context.FishingSpots.AddRange(
            new FishingSpot
            {
                Name = "Danube Delta",
                Latitude = 45.1667,
                Longitude = 29.6000,
                PricePerHour = 2.5m,
                UserId = existingSeededAdmin.Id
            },
            new FishingSpot
            {
                Name = "Custom Existing Spot",
                Latitude = 46,
                Longitude = 26,
                PricePerHour = 15m,
                UserId = customUser.Id
            });
        await _context.SaveChangesAsync();

        await DatabaseInitializer.SeedMissingDataAsync(_context, NullLogger.Instance);

        Assert.Equal(11, await _context.Users.CountAsync());
        Assert.Equal(27, await _context.FishingSpots.CountAsync());
        Assert.Equal(1, await _context.Users.CountAsync(user => user.Username == "admin"));
        Assert.Equal(1, await _context.FishingSpots.CountAsync(spot => spot.Name == "Danube Delta"));
        Assert.Equal(1, await _context.FishingSpots.CountAsync(spot => spot.Name == "Custom Existing Spot"));
        Assert.True(await _context.FishingSpots.AnyAsync(spot => spot.Name == "Balta Catrunesti 3"));
    }

    [Fact]
    public async Task SeedMissingDataAsync_WhenCalledTwice_DoesNotDuplicateSeedRecords()
    {
        await DatabaseInitializer.SeedMissingDataAsync(_context, NullLogger.Instance);
        await DatabaseInitializer.SeedMissingDataAsync(_context, NullLogger.Instance);

        Assert.Equal(10, await _context.Users.CountAsync());
        Assert.Equal(26, await _context.FishingSpots.CountAsync());
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}