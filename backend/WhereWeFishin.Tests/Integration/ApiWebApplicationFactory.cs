using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WhereWeFishin.Core.Interfaces;
using WhereWeFishin.Database.Context;

namespace WhereWeFishin.Tests.Integration;

public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private static readonly InMemoryDatabaseRoot SharedDatabaseRoot = new();
    private readonly string _databaseName = $"wherewefishin-integration-{Guid.NewGuid():N}";

    public ApiWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "IntegrationTests");
        Environment.SetEnvironmentVariable("Jwt__Key", "integration-tests-secret-key-that-is-long-enough");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "WhereWeFishin.Tests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "WhereWeFishin.Tests.Users");
        Environment.SetEnvironmentVariable("Jwt__ExpirationHours", "24");
        Environment.SetEnvironmentVariable("FishRecognitionService__Url", "http://localhost:5001");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTesting");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "DataSource=:memory:",
                ["Jwt:Key"] = "integration-tests-secret-key-that-is-long-enough",
                ["Jwt:Issuer"] = "WhereWeFishin.Tests",
                ["Jwt:Audience"] = "WhereWeFishin.Tests.Users",
                ["Jwt:ExpirationHours"] = "24",
                ["FishRecognitionService:Url"] = "http://localhost:5001"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.RemoveAll<IEmailService>();

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName, SharedDatabaseRoot);
            });

            services.AddScoped<IEmailService, NoOpEmailService>();
        });
    }

    public async Task<TResult> ExecuteDbContextAsync<TResult>(Func<ApplicationDbContext, Task<TResult>> action)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await action(context);
    }

    private sealed class NoOpEmailService : IEmailService
    {
        public Task SendWelcomeEmailAsync(string toEmail, string? firstName) => Task.CompletedTask;

        public Task SendBookingConfirmationEmailAsync(
            string toEmail,
            string? firstName,
            string spotName,
            DateTime startDateUtc,
            int durationHours,
            decimal totalPrice,
            int bookingId) => Task.CompletedTask;

        public Task SendPasswordResetEmailAsync(string toEmail, string? firstName, string resetCode)
            => Task.CompletedTask;
    }
}