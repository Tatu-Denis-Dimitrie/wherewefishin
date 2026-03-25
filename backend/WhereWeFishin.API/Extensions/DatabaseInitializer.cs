using WhereWeFishin.Database.Context;
using Microsoft.EntityFrameworkCore;

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
            logger.LogInformation("Applying database migrations...");
            await context.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully.");

            if (context.Users.Any())
            {
                logger.LogInformation("Database already has data - skipping seeding.");
            }
            else
            {
                logger.LogInformation("Database is empty - starting seeding...");

                var users = WhereWeFishin.Database.MockData.SeedData.GetUsers();
                await context.Users.AddRangeAsync(users);
                await context.SaveChangesAsync();
                logger.LogInformation("Added {Count} users", users.Count);

                var userIds = users.Select(u => u.Id).ToList();
                var fishingSpots = WhereWeFishin.Database.MockData.SeedData.GetFishingSpots(userIds);
                await context.FishingSpots.AddRangeAsync(fishingSpots);
                await context.SaveChangesAsync();
                logger.LogInformation("Added {Count} fishing spots", fishingSpots.Count);

                var spotIds = fishingSpots.Select(f => f.Id).ToList();
                var catches = WhereWeFishin.Database.MockData.SeedData.GetCatches(userIds, spotIds);
                await context.Catches.AddRangeAsync(catches);
                await context.SaveChangesAsync();
                logger.LogInformation("Added {Count} catches", catches.Count);

                logger.LogInformation("Seeding completed! TEST ACCOUNTS:");
                logger.LogInformation("  Admin: admin / admin123");
                logger.LogInformation("  Manager: manager1, manager2 / manager123");
                logger.LogInformation("  Users: ion_fisher, maria_fisher, etc. / password123");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during database startup.");
            throw;
        }
    }
}
