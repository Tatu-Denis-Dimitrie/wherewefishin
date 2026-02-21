using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Interfaces;
using WhereWeFishin.Core.Services;
using WhereWeFishin.Database.Context;
using WhereWeFishin.Database.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger/OpenAPI
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "WhereWeFishin API", 
        Version = "v1",
        Description = "API pentru aplicația WhereWeFishin - gestionarea locațiilor de pescuit și capturilor"
    });

    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Configure Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlServerOptionsAction: sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        }));

// Configure JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT Issuer not configured");
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? throw new InvalidOperationException("JWT Audience not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()
              .WithExposedHeaders("Content-Length", "Content-Range", "Accept-Ranges");
    });
});

// Register Repositories
builder.Services.AddScoped<IRepository<User>, Repository<User>>();
builder.Services.AddScoped<IRepository<FishingSpot>, FishingSpotRepository>();
builder.Services.AddScoped<IRepository<Catch>, Repository<Catch>>();
builder.Services.AddScoped<IRepository<VideoAnalysis>, Repository<VideoAnalysis>>();
builder.Services.AddScoped<IRepository<FishingSession>, Repository<FishingSession>>();

// Register Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddHttpClient<IFishRecognitionService, FishRecognitionService>(client =>
{
    var fishServiceUrl = builder.Configuration["FishRecognitionService:Url"] 
        ?? throw new InvalidOperationException("Fish Recognition Service URL not configured");
    client.BaseAddress = new Uri(fishServiceUrl);
    client.Timeout = TimeSpan.FromMinutes(20); // For video processing - increased for longer videos
});

// Register HttpClientFactory for general use
builder.Services.AddHttpClient();

var app = builder.Build();

// Seed database with test data - only if SEED_DATABASE environment variable is set to "true"
var seedDatabase = builder.Configuration.GetValue<bool>("SeedDatabase", false);
if (seedDatabase)
{
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            var logger = services.GetRequiredService<ILogger<Program>>();
            
            logger.LogInformation("Starting database seeding...");
            
            // Delete existing data in the correct order (respecting foreign keys)
            await context.Database.ExecuteSqlRawAsync("DELETE FROM VideoAnalyses");
            await context.Database.ExecuteSqlRawAsync("DELETE FROM Catches");
            await context.Database.ExecuteSqlRawAsync("DELETE FROM FishingSpots");
            await context.Database.ExecuteSqlRawAsync("DELETE FROM Users");
            
            // Reset identity columns
            await context.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('Users', RESEED, 0)");
            await context.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('FishingSpots', RESEED, 0)");
            await context.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('Catches', RESEED, 0)");
            await context.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('VideoAnalyses', RESEED, 0)");
            
            logger.LogInformation("Existing data cleared.");
            
            // Add seed data
            var users = WhereWeFishin.Database.MockData.SeedData.GetUsers();
            await context.Users.AddRangeAsync(users);
            await context.SaveChangesAsync();
            
            logger.LogInformation("Added {Count} users", users.Count);
            
            // Get user IDs for fishing spots
            var userIds = context.Users.Select(u => u.Id).ToList();
            var fishingSpots = WhereWeFishin.Database.MockData.SeedData.GetFishingSpots(userIds);
            await context.FishingSpots.AddRangeAsync(fishingSpots);
            await context.SaveChangesAsync();
            
            logger.LogInformation("Added {Count} fishing spots", fishingSpots.Count);
            
            // Get fishing spot and user IDs for catches
            var spotIds = context.FishingSpots.Select(f => f.Id).ToList();
            var catches = WhereWeFishin.Database.MockData.SeedData.GetCatches(userIds, spotIds);
            await context.Catches.AddRangeAsync(catches);
            await context.SaveChangesAsync();
            
            logger.LogInformation("Added {Count} catches", catches.Count);
            
            logger.LogInformation("Database seeding completed successfully!");
            logger.LogInformation("TEST ACCOUNTS:");
            logger.LogInformation("  Admin: admin / admin123");
            logger.LogInformation("  Manager: manager1, manager2 / manager123");
            logger.LogInformation("  Users: ion_pescar, maria_fisher, etc. / password123");
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "WhereWeFishin API v1");
        c.RoutePrefix = string.Empty; // Set Swagger UI at app's root
    });
}

app.UseHttpsRedirection();

app.UseCors("AllowAngularApp");

// Ensure uploads directory exists
var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

// Serve static files from uploads directory - MUST be before Authentication/Authorization
var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
provider.Mappings[".mp4"] = "video/mp4";
provider.Mappings[".avi"] = "video/x-msvideo";
provider.Mappings[".mov"] = "video/quicktime";
provider.Mappings[".mkv"] = "video/x-matroska";

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads",
    ContentTypeProvider = provider,
    ServeUnknownFileTypes = false,
    OnPrepareResponse = ctx =>
    {
        // Enable CORS for video files
        ctx.Context.Response.Headers["Access-Control-Allow-Origin"] = "*";
        ctx.Context.Response.Headers["Access-Control-Allow-Methods"] = "GET, HEAD, OPTIONS";
        ctx.Context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Range";
        ctx.Context.Response.Headers["Access-Control-Expose-Headers"] = "Content-Length, Content-Range";
        
        // Enable partial content support for video streaming
        ctx.Context.Response.Headers["Accept-Ranges"] = "bytes";
        
        // Cache for 1 hour
        ctx.Context.Response.Headers["Cache-Control"] = "public, max-age=3600";
    }
});

// Authentication/Authorization middleware - after static files
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
