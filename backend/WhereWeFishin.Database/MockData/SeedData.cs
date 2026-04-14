using BCrypt.Net;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Enums;

namespace WhereWeFishin.Database.MockData;

/// <summary>
/// Test data for seeding the database
/// 
/// TEST ACCOUNTS:
/// ==============
/// Admin:
///   - Username: admin | Password: admin123 | Email: admin@wherewefishin.com
///   - Full access: user management, statistics, all features
/// 
/// Managers:
///   - Username: manager1 | Password: manager123 | Email: manager1@wherewefishin.com
///   - Username: manager2 | Password: manager123 | Email: manager2@wherewefishin.com
///   - Access: add/remove fishing spots
/// 
/// Users:
///   - Username: ion_fisher, maria_fisher, andrei_fishing, petre_balanescu, 
///              carmen_nistor, dan_cretu, adriana_dobre
///   - Password: password123 (for all)
///   - Access: view spots, video analysis
/// </summary>
public static class SeedData
{
    public static List<User> GetUsers()
    {
        var adminHash = BCrypt.Net.BCrypt.HashPassword("admin123");
        var managerHash = BCrypt.Net.BCrypt.HashPassword("manager123");
        var userHash = BCrypt.Net.BCrypt.HashPassword("password123");

        return new List<User>
        {
            // Admin account
            new User
            {
                Username = "admin",
                Email = "admin@wherewefishin.com",
                PasswordHash = adminHash,
                FirstName = "Gimi",
                LastName = "Sefu",
                Role = UserRole.Admin,
                CreatedAt = DateTime.UtcNow.AddYears(-1),
                UpdatedAt = DateTime.UtcNow
            },
            
            // Manager accounts
            new User
            {
                Username = "manager1",
                Email = "manager1@wherewefishin.com",
                PasswordHash = managerHash,
                FirstName = "George",
                LastName = "Marinescu",
                Role = UserRole.Manager,
                CreatedAt = DateTime.UtcNow.AddMonths(-10),
                UpdatedAt = DateTime.UtcNow
            },
            new User
            {
                Username = "manager2",
                Email = "manager2@wherewefishin.com",
                PasswordHash = managerHash,
                FirstName = "Elena",
                LastName = "Vasilescu",
                Role = UserRole.Manager,
                CreatedAt = DateTime.UtcNow.AddMonths(-8),
                UpdatedAt = DateTime.UtcNow
            },
            
            // Regular user accounts
            new User
            {
                Username = "ion_fisher",
                Email = "ion@email.com",
                PasswordHash = userHash,
                FirstName = "Ion",
                LastName = "Popescu",
                Role = UserRole.User,
                CreatedAt = DateTime.UtcNow.AddMonths(-6),
                UpdatedAt = DateTime.UtcNow.AddMonths(-2)
            },
            new User
            {
                Username = "maria_fisher",
                Email = "maria@email.com",
                PasswordHash = userHash,
                FirstName = "Maria",
                LastName = "Ionescu",
                Role = UserRole.User,
                CreatedAt = DateTime.UtcNow.AddMonths(-5),
                UpdatedAt = DateTime.UtcNow.AddMonths(-1)
            },
            new User
            {
                Username = "andrei_fishing",
                Email = "andrei@email.com",
                PasswordHash = userHash,
                FirstName = "Andrei",
                LastName = "Popa",
                Role = UserRole.User,
                CreatedAt = DateTime.UtcNow.AddMonths(-4),
                UpdatedAt = DateTime.UtcNow.AddDays(-10)
            },
            new User
            {
                Username = "petre_balanescu",
                Email = "petre@email.com",
                PasswordHash = userHash,
                FirstName = "Petre",
                LastName = "Balanescu",
                Role = UserRole.User,
                CreatedAt = DateTime.UtcNow.AddMonths(-3),
                UpdatedAt = DateTime.UtcNow.AddDays(-5)
            },
            new User
            {
                Username = "carmen_nistor",
                Email = "carmen@email.com",
                PasswordHash = userHash,
                FirstName = "Carmen",
                LastName = "Nistor",
                Role = UserRole.User,
                CreatedAt = DateTime.UtcNow.AddMonths(-2),
                UpdatedAt = DateTime.UtcNow.AddDays(-3)
            },
            new User
            {
                Username = "dan_cretu",
                Email = "dan@email.com",
                PasswordHash = userHash,
                FirstName = "Dan",
                LastName = "Cretu",
                Role = UserRole.User,
                CreatedAt = DateTime.UtcNow.AddMonths(-1),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new User
            {
                Username = "adriana_dobre",
                Email = "adriana@email.com",
                PasswordHash = userHash,
                FirstName = "Adriana",
                LastName = "Dobre",
                Role = UserRole.User,
                CreatedAt = DateTime.UtcNow.AddDays(-20),
                UpdatedAt = DateTime.UtcNow
            }
        };
    }

    public static List<FishingSpot> GetFishingSpots(List<int> userIds)
    {
        return new List<FishingSpot>
        {
            new FishingSpot
            {
                Name = "Snagov Lake",
                Description = "Great fishing spot with rich vegetation",
                Latitude = 44.7044,
                Longitude = 26.1496,
                UserId = userIds[0],
                CreatedAt = DateTime.UtcNow.AddMonths(-5),
                PricePerHour = 1.0m

            },
            new FishingSpot
            {
                Name = "Danube Delta",
                Description = "Angler's paradise with high biodiversity",
                Latitude = 45.1667,
                Longitude = 29.6000,
                UserId = userIds[1],
                CreatedAt = DateTime.UtcNow.AddMonths(-2),
                PricePerHour = 2.5m
            },
            new FishingSpot
            {
                Name = "Vidraru Dam",
                Latitude = 45.3500,
                Longitude = 24.6333,
                UserId = userIds[0],
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                PricePerHour = 1.5m
            },
            new FishingSpot
            {
                Name = "Bicaz Lake",
                Description = "Trout and golden chub, very clean water",
                Latitude = 46.9167,
                Longitude = 25.8500,
                UserId = userIds[2],
                CreatedAt = DateTime.UtcNow.AddDays(-15),
                PricePerHour = 2.0m
            }
        };
    }
}
