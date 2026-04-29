using WhereWeFishin.Database.Context;
using Microsoft.EntityFrameworkCore;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Database.MockData;

namespace WhereWeFishin.API.Extensions;

public static class DatabaseInitializer
{
    private sealed record ExistingSeedUser(int Id, string Username, string Email);

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
        var existingSeededUsers = await GetExistingSeedUsersAsync(context, seededUsers);
        var existingSeededUserIdsByUsername = BuildSeededUserIdsByUsername(existingSeededUsers, seededUsers);

        if (await context.Users.AnyAsync())
            {
            logger.LogInformation("Database already has data - checking for missing seed records.");
        }
        else
        {
            logger.LogInformation("Database is empty - starting seeding...");
        }

        var usersToAdd = seededUsers
            .Where(user => !existingSeededUserIdsByUsername.ContainsKey(user.Username))
            .ToList();

        if (usersToAdd.Count > 0)
        {
            await context.Users.AddRangeAsync(usersToAdd);
            await context.SaveChangesAsync();
            logger.LogInformation("Added {Count} users", usersToAdd.Count);

            existingSeededUsers = await GetExistingSeedUsersAsync(context, seededUsers);
        }

        var seededUserIdsByUsername = BuildSeededUserIdsByUsername(existingSeededUsers, seededUsers);

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

    private static async Task<List<ExistingSeedUser>> GetExistingSeedUsersAsync(
        ApplicationDbContext context,
        IReadOnlyCollection<User> seededUsers)
    {
        var seededUsernames = seededUsers.Select(user => user.Username).ToList();
        var seededEmails = seededUsers.Select(user => user.Email).ToList();

        return await context.Users
            .Where(user => seededUsernames.Contains(user.Username) || seededEmails.Contains(user.Email))
            .Select(user => new ExistingSeedUser(user.Id, user.Username, user.Email))
            .ToListAsync();
    }

    private static Dictionary<string, int> BuildSeededUserIdsByUsername(
        IReadOnlyCollection<ExistingSeedUser> existingUsers,
        IReadOnlyCollection<User> seededUsers)
    {
        var existingUsersByUsername = existingUsers
            .ToDictionary(user => user.Username, user => user, StringComparer.OrdinalIgnoreCase);
        var existingUsersByEmail = existingUsers
            .ToDictionary(user => user.Email, user => user, StringComparer.OrdinalIgnoreCase);
        var seededUserIdsByUsername = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var seededUser in seededUsers)
        {
            existingUsersByUsername.TryGetValue(seededUser.Username, out var usernameMatch);
            existingUsersByEmail.TryGetValue(seededUser.Email, out var emailMatch);

            if (usernameMatch is not null && emailMatch is not null && usernameMatch.Id != emailMatch.Id)
            {
                throw new InvalidOperationException(
                    $"Seed user '{seededUser.Username}' matches different existing users by username and email.");
            }

            var matchedUser = usernameMatch ?? emailMatch;
            if (matchedUser is not null)
            {
                seededUserIdsByUsername[seededUser.Username] = matchedUser.Id;
            }
        }

        return seededUserIdsByUsername;
    }
}
