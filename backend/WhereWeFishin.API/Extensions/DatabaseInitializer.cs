using WhereWeFishin.Database.Context;
using Microsoft.EntityFrameworkCore;
using WhereWeFishin.Database.MockData;

namespace WhereWeFishin.API.Extensions;

public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        try
        {
            if (app.Environment.IsEnvironment("IntegrationTesting"))
            {
                logger.LogInformation("Creating integration test database schema...");
                await context.Database.EnsureCreatedAsync();
                logger.LogInformation("Integration test database schema created successfully.");
            }
            else
            {
                logger.LogInformation("Applying database migrations...");
                await context.Database.MigrateAsync();
                logger.LogInformation("Database migrations applied successfully.");
            }

            await SeedMissingDataAsync(context, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during database startup.");
            throw;
        }
    }

    public static async Task SeedMissingDataAsync(ApplicationDbContext context, ILogger logger)
    {
        var seededUsers = SeedData.GetUsers();
        var seededUsernames = seededUsers.Select(user => user.Username).ToList();

        if (await context.Users.AnyAsync())
            {
            logger.LogInformation("Database already has data - checking for missing seed records.");
        }
        else
        {
            logger.LogInformation("Database is empty - starting seeding...");
        }

        var existingSeededUsernames = (await context.Users
            .Where(user => seededUsernames.Contains(user.Username))
            .Select(user => user.Username)
            .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var usersToAdd = seededUsers
            .Where(user => !existingSeededUsernames.Contains(user.Username))
            .ToList();

        if (usersToAdd.Count > 0)
        {
            await context.Users.AddRangeAsync(usersToAdd);
            await context.SaveChangesAsync();
            logger.LogInformation("Added {Count} users", usersToAdd.Count);
        }

        var seededUserIdsByUsername = (await context.Users
            .Where(user => seededUsernames.Contains(user.Username))
            .Select(user => new { user.Id, user.Username })
            .ToListAsync())
            .ToDictionary(user => user.Username, user => user.Id, StringComparer.OrdinalIgnoreCase);

        var orderedSeededUserIds = seededUsernames
            .Select(username => seededUserIdsByUsername.TryGetValue(username, out var userId)
                ? userId
                : throw new InvalidOperationException($"Missing seeded user '{username}' after seeding."))
            .ToList();

        var seededFishingSpots = SeedData.GetFishingSpots(orderedSeededUserIds);
        var seededSpotNames = seededFishingSpots.Select(spot => spot.Name).ToList();

        var existingSeededSpotNames = (await context.FishingSpots
            .Where(spot => seededSpotNames.Contains(spot.Name))
            .Select(spot => spot.Name)
            .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var spotsToAdd = seededFishingSpots
            .Where(spot => !existingSeededSpotNames.Contains(spot.Name))
            .ToList();

        if (spotsToAdd.Count > 0)
        {
            await context.FishingSpots.AddRangeAsync(spotsToAdd);
            await context.SaveChangesAsync();
            logger.LogInformation("Added {Count} fishing spots", spotsToAdd.Count);
        }

        if (usersToAdd.Count == 0 && spotsToAdd.Count == 0)
        {
            logger.LogInformation("Database already contains all seeded users and fishing spots.");
            return;
        }

        logger.LogInformation("Seeding completed! TEST ACCOUNTS:");
        logger.LogInformation("  Admin: admin / admin123");
        logger.LogInformation("  Manager: manager1, manager2 / manager123");
        logger.LogInformation("  Users: ion_fisher, maria_fisher, etc. / password123");
    }
}
