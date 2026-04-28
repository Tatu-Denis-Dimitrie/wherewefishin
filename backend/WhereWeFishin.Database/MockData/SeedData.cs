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
        FishingSpot CreateSeededSpot(
            string name,
            string? description,
            double latitude,
            double longitude,
            int userId,
            int daysAgo,
            decimal pricePerHour = 1.0m)
            => new()
            {
                Name = name,
                Description = description,
                Latitude = latitude,
                Longitude = longitude,
                UserId = userId,
                CreatedAt = DateTime.UtcNow.AddDays(-daysAgo),
                PricePerHour = pricePerHour
            };

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
            },
            CreateSeededSpot(
                "Balta Cornu",
                "Recreational fishing lake in Dolj with a quiet natural setting.",
                44.2173413,
                23.293206,
                userIds[1],
                22),
            CreateSeededSpot(
                "Balta Catrunesti 3",
                "Private lake with gazebos and nonstop access for sport fishing.",
                44.5520703,
                26.4043805,
                userIds[1],
                21),
            CreateSeededSpot(
                "Balta Durnesti",
                "Managed fishing lake in Botosani for relaxed sport fishing sessions.",
                47.7641651,
                27.0943719,
                userIds[2],
                20),
            CreateSeededSpot(
                "Balta Lin Lake",
                "Premium catch-and-release lake near Satu Mare.",
                47.7484956,
                22.8429812,
                userIds[2],
                19),
            CreateSeededSpot(
                "Iaz A' la Miruna",
                "Peaceful family-friendly lake near Valea Alba.",
                47.0499733,
                26.5146037,
                userIds[0],
                18),
            CreateSeededSpot(
                "Balta Poienita Agrotur",
                "Private agrotourism lake surrounded by orchards and woodland.",
                47.6567915,
                26.8664837,
                userIds[1],
                17),
            CreateSeededSpot(
                "Iazul Buliga",
                "Scenic fishing lake reopened for sport anglers in Suceava.",
                47.8448588,
                25.9412461,
                userIds[2],
                16),
            CreateSeededSpot(
                "New Carp Lake",
                "Carp-focused lake with a quiet setup near Belin.",
                45.9339961,
                25.5544336,
                userIds[0],
                15),
            CreateSeededSpot(
                "Iaz Totoesti",
                "Village lake in Erbiceni suited for casual fishing trips.",
                47.253347,
                27.294812,
                userIds[1],
                14),
            CreateSeededSpot(
                "Arena Pescarilor",
                "Organized catch-and-release lake near Sagu.",
                46.0674036,
                21.2850525,
                userIds[2],
                13),
            CreateSeededSpot(
                "Iaz Santa",
                "Quiet fishing lake near Santa Mare with easy day access.",
                47.6070308,
                27.3190196,
                userIds[0],
                12),
            CreateSeededSpot(
                "Lacul 5 Lazuri",
                "Catch-and-release lake near Lazuri built for serious fishing sessions.",
                44.8888045,
                25.5684123,
                userIds[1],
                11),
            CreateSeededSpot(
                "ENPI Lake Fishing",
                "Sport fishing lake near Iasi with a modern setup.",
                47.2183899,
                27.5886977,
                userIds[2],
                10),
            CreateSeededSpot(
                "Balta Nadas",
                "Well-kept catch-and-release lake in Arad County.",
                46.213468,
                21.8928278,
                userIds[0],
                9),
            CreateSeededSpot(
                "Iaz Brehuiesti",
                "Fishing lake with solid access and a reputation for larger fish.",
                47.6829607,
                26.5246466,
                userIds[1],
                8),
            CreateSeededSpot(
                "Balta Chiroiu 3",
                "Large lake in Ialomita with varied depth and long banks.",
                44.5889391,
                26.5036798,
                userIds[2],
                7),
            CreateSeededSpot(
                "Iaz Moimesti",
                "Nonstop fishing lake near Popricani.",
                47.2683092,
                27.5140441,
                userIds[0],
                6),
            CreateSeededSpot(
                "Iaz Brosteni",
                "Fishing lake in Botosani known for carp and crucian catches.",
                47.7576079,
                27.0960554,
                userIds[1],
                5),
            CreateSeededSpot(
                "Iaz Romani",
                "Local fishing lake in Neamt with an easy-access shoreline.",
                46.7960272,
                26.6940764,
                userIds[2],
                4),
            CreateSeededSpot(
                "Iaz Dienet",
                "Amenity-focused lake with direct car access to the bank.",
                46.3323911,
                27.0544831,
                userIds[0],
                3),
            CreateSeededSpot(
                "Balta Podari Bazin 2",
                "Sport fishing lake in Calarasi with room for longer sessions.",
                44.484471,
                26.6425192,
                userIds[1],
                2),
            CreateSeededSpot(
                "Iazul Climauti",
                "Fishing and picnic lake in northern Suceava.",
                47.9543701,
                25.9249954,
                userIds[2],
                1)
        };
    }
}
