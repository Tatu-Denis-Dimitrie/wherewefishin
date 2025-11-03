using Microsoft.EntityFrameworkCore;
using WhereWeFishin.Core.Interfaces;
using WhereWeFishin.Database.Context;
using WhereWeFishin.Database.MockData;
using WhereWeFishin.Database.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configure Entity Framework and SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure()
    )
);

// Register repositories and Unit of Work
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "WhereWeFishin API",
        Version = "v1",
        Description = "API for WhereWeFishin fishing spots and catches management"
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "WhereWeFishin API V1");
    });
}

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

// Seed database with mock data in Development
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Ensure database is created
        context.Database.EnsureCreated();

        // Check if data already exists
        if (!context.Users.Any())
        {
            // Add users
            var users = SeedData.GetUsers();
            context.Users.AddRange(users);
            context.SaveChanges();

            // Add fishing spots
            var fishingSpots = SeedData.GetFishingSpots();
            context.FishingSpots.AddRange(fishingSpots);
            context.SaveChanges();

            // Add catches
            var catches = SeedData.GetCatches();
            context.Catches.AddRange(catches);
            context.SaveChanges();

            Console.WriteLine("Mock data seeded successfully!");
        }
        else
        {
            Console.WriteLine("Database already contains data. Skipping seed.");
        }
    }
}

app.Run();
