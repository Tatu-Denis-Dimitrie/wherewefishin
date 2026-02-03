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
                Username = "ion_pescar",
                Email = "ion@email.com",
                PasswordHash = "password", // Parola simplă
                FirstName = "Ion",
                LastName = "Popescu",
                Role = "User",
                CreatedAt = DateTime.UtcNow.AddMonths(-6)
            },
            new User
            {
                Username = "maria_fisher",
                Email = "maria@email.com",
                PasswordHash = "password", // Parola simplă
                FirstName = "Maria",
                LastName = "Ionescu",
                Role = "User",
                CreatedAt = DateTime.UtcNow.AddMonths(-3)
            },
            new User
            {
                Username = "andrei_pescuit",
                Email = "andrei@email.com",
                PasswordHash = "password", // Parola simplă
                FirstName = "Andrei",
                LastName = "Popa",
                Role = "User",
                CreatedAt = DateTime.UtcNow.AddMonths(-1)
            },
            new User
            {
                Username = "admin",
                Email = "admin@email.com",
                PasswordHash = "admin123", // Parola pt Admin
                FirstName = "Admin",
                LastName = "System",
                Role = "Admin",
                CreatedAt = DateTime.UtcNow
            },
            new User
            {
                Username = "manager",
                Email = "manager@email.com",
                PasswordHash = "manager123", // Parola pt Manager
                FirstName = "Manager",
                LastName = "System",
                Role = "Manager",
                CreatedAt = DateTime.UtcNow
            }
        };
    }

    public static List<FishingSpot> GetFishingSpots(List<int> userIds)
    {
        return new List<FishingSpot>
        {
            new FishingSpot
            {
                Name = "Lacul Snagov",
                Description = "Loc excelent pentru pescuit, cu vegetatie bogata",
                Latitude = 44.7044,
                Longitude = 26.1496,
                UserId = userIds[0],
                CreatedAt = DateTime.UtcNow.AddMonths(-5)
            },
            new FishingSpot
            {
                Name = "Delta Dunarii",
                Description = "Paradis pentru pescari, biodiversitate mare",
                Latitude = 45.1667,
                Longitude = 29.6000,
                UserId = userIds[1],
                CreatedAt = DateTime.UtcNow.AddMonths(-2)
            },
            new FishingSpot
            {
                Name = "Barajul Vidraru",
                Latitude = 45.3500,
                Longitude = 24.6333,
                UserId = userIds[0],
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            },
            new FishingSpot
            {
                Name = "Lacul Bicaz",
                Description = "Pastrav si clean auriu, apa foarte curata",
                Latitude = 46.9167,
                Longitude = 25.8500,
                UserId = userIds[2],
                CreatedAt = DateTime.UtcNow.AddDays(-15)
            }
        };
    }

    public static List<Catch> GetCatches(List<int> userIds, List<int> spotIds)
    {
        return new List<Catch>
        {
            new Catch
            {
                FishSpecies = "Crap",
                Weight = 3.5,
                Length = 45.0,
                CaughtAt = DateTime.UtcNow.AddDays(-10),
                Notes = "Prins dimineata devreme",
                UserId = userIds[0],
                FishingSpotId = spotIds[0],
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            },
            new Catch
            {
                FishSpecies = "Stiuca",
                Weight = 5.2,
                Length = 68.0,
                CaughtAt = DateTime.UtcNow.AddDays(-5),
                Notes = "Foarte agresiva",
                UserId = userIds[1],
                FishingSpotId = spotIds[1],
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            },
            new Catch
            {
                FishSpecies = "Pastrav",
                Weight = 1.8,
                Length = 35.0,
                CaughtAt = DateTime.UtcNow.AddDays(-3),
                UserId = userIds[0],
                FishingSpotId = spotIds[2],
                CreatedAt = DateTime.UtcNow.AddDays(-3)
            },
            new Catch
            {
                FishSpecies = "Somn",
                Weight = 12.5,
                Length = 95.0,
                CaughtAt = DateTime.UtcNow.AddDays(-1),
                Notes = "Record personal!",
                UserId = userIds[2],
                FishingSpotId = spotIds[3],
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new Catch
            {
                FishSpecies = "Clean",
                Weight = 2.1,
                Length = 38.0,
                CaughtAt = DateTime.UtcNow.AddHours(-12),
                UserId = userIds[1],
                FishingSpotId = spotIds[1],
                CreatedAt = DateTime.UtcNow.AddHours(-12)
            }
        };
    }
}
