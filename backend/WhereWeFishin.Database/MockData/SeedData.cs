using WhereWeFishin.Core.Entities;

namespace WhereWeFishin.Database.MockData;

public static class SeedData
{
    public static List<User> GetUsers()
    {
        return new List<User>
        {
            new User
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Username = "ion_pescar",
                Email = "ion@email.com",
                PasswordHash = "hash123",
                FirstName = "Ion",
                LastName = "Popescu",
                CreatedAt = DateTime.UtcNow.AddMonths(-6)
            },
            new User
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                Username = "maria_fisher",
                Email = "maria@email.com",
                PasswordHash = "hash456",
                FirstName = "Maria",
                LastName = "Ionescu",
                CreatedAt = DateTime.UtcNow.AddMonths(-3)
            },
            new User
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                Username = "andrei_pescuit",
                Email = "andrei@email.com",
                PasswordHash = "hash789",
                FirstName = "Andrei",
                LastName = "Popa",
                CreatedAt = DateTime.UtcNow.AddMonths(-1)
            }
        };
    }

    public static List<FishingSpot> GetFishingSpots()
    {
        return new List<FishingSpot>
        {
            new FishingSpot
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Name = "Lacul Snagov",
                Description = "Loc excelent pentru pescuit, cu vegetatie bogata",
                Latitude = 44.7044,
                Longitude = 26.1496,
                UserId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                CreatedAt = DateTime.UtcNow.AddMonths(-5)
            },
            new FishingSpot
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                Name = "Delta Dunarii",
                Description = "Paradis pentru pescari, biodiversitate mare",
                Latitude = 45.1667,
                Longitude = 29.6000,
                UserId = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                CreatedAt = DateTime.UtcNow.AddMonths(-2)
            },
            new FishingSpot
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                Name = "Barajul Vidraru",
                Latitude = 45.3500,
                Longitude = 24.6333,
                UserId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            },
            new FishingSpot
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000004"),
                Name = "Lacul Bicaz",
                Description = "Pastrav si clean auriu, apa foarte curata",
                Latitude = 46.9167,
                Longitude = 25.8500,
                UserId = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                CreatedAt = DateTime.UtcNow.AddDays(-15)
            }
        };
    }

    public static List<Catch> GetCatches()
    {
        return new List<Catch>
        {
            new Catch
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                FishSpecies = "Crap",
                Weight = 3.5,
                Length = 45.0,
                CaughtAt = DateTime.UtcNow.AddDays(-10),
                Notes = "Prins dimineata devreme",
                UserId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                FishingSpotId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            },
            new Catch
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                FishSpecies = "Stiuca",
                Weight = 5.2,
                Length = 68.0,
                CaughtAt = DateTime.UtcNow.AddDays(-5),
                Notes = "Foarte agresiva",
                UserId = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                FishingSpotId = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            },
            new Catch
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                FishSpecies = "Pastrav",
                Weight = 1.8,
                Length = 35.0,
                CaughtAt = DateTime.UtcNow.AddDays(-3),
                UserId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                FishingSpotId = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                CreatedAt = DateTime.UtcNow.AddDays(-3)
            },
            new Catch
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000004"),
                FishSpecies = "Somn",
                Weight = 12.5,
                Length = 95.0,
                CaughtAt = DateTime.UtcNow.AddDays(-1),
                Notes = "Record personal!",
                UserId = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                FishingSpotId = Guid.Parse("00000000-0000-0000-0000-000000000004"),
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new Catch
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000005"),
                FishSpecies = "Clean",
                Weight = 2.1,
                Length = 38.0,
                CaughtAt = DateTime.UtcNow.AddHours(-12),
                UserId = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                FishingSpotId = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                CreatedAt = DateTime.UtcNow.AddHours(-12)
            }
        };
    }
}
