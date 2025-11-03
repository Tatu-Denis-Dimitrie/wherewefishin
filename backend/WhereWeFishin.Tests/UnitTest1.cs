using WhereWeFishin.Core.Entities;

namespace WhereWeFishin.Tests;

public class EntityTests
{
    [Fact]
    public void User_ShouldCreateWithDefaultValues()
    {
        // Arrange & Act
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = "hash123"
        };

        // Assert
        Assert.Equal(userId, user.Id);
        Assert.Equal("testuser", user.Username);
        Assert.Equal("test@example.com", user.Email);
        Assert.NotNull(user.FishingSpots);
        Assert.NotNull(user.Catches);
    }

    [Fact]
    public void FishingSpot_ShouldCreateWithRequiredProperties()
    {
        // Arrange & Act
        var spotId = Guid.NewGuid();
        var spot = new FishingSpot
        {
            Id = spotId,
            Name = "Best Fishing Spot",
            Latitude = 45.123456,
            Longitude = 25.654321,
            UserId = Guid.NewGuid()
        };

        // Assert
        Assert.Equal(spotId, spot.Id);
        Assert.Equal("Best Fishing Spot", spot.Name);
        Assert.Equal(45.123456, spot.Latitude);
        Assert.Equal(25.654321, spot.Longitude);
        Assert.NotNull(spot.Catches);
    }

    [Fact]
    public void Catch_ShouldCreateWithRequiredProperties()
    {
        // Arrange & Act
        var catchId = Guid.NewGuid();
        var catchEntity = new Catch
        {
            Id = catchId,
            FishSpecies = "Trout",
            Weight = 2.5,
            Length = 45.0,
            CaughtAt = DateTime.UtcNow,
            UserId = Guid.NewGuid(),
            FishingSpotId = Guid.NewGuid()
        };

        // Assert
        Assert.Equal(catchId, catchEntity.Id);
        Assert.Equal("Trout", catchEntity.FishSpecies);
        Assert.Equal(2.5, catchEntity.Weight);
        Assert.Equal(45.0, catchEntity.Length);
    }
}
