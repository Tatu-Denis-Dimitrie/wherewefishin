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

// Configure Kestrel to accept larger files (up to 150MB)
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 150 * 1024 * 1024; // 150MB
});

// Add services to the container
builder.Services.AddControllers(options =>
{
    options.MaxModelBindingCollectionSize = int.MaxValue;
})
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
});
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
        policy.WithOrigins(
                  "http://localhost:4200",  // local Angular dev server
                  "http://localhost",        // Docker: nginx on port 80
                  "http://localhost:80"      // Docker: explicit port
              )
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
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
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

// Apply EF Core migrations automatically on startup (creates DB if it doesn't exist)
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        logger.LogInformation("Applying database migrations...");
        await context.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while applying database migrations.");
        throw;
    }
}

// Seed database automatically if empty
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
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

            var userIds = context.Users.Select(u => u.Id).ToList();
            var fishingSpots = WhereWeFishin.Database.MockData.SeedData.GetFishingSpots(userIds);
            await context.FishingSpots.AddRangeAsync(fishingSpots);
            await context.SaveChangesAsync();
            logger.LogInformation("Added {Count} fishing spots", fishingSpots.Count);

            var spotIds = context.FishingSpots.Select(f => f.Id).ToList();
            var catches = WhereWeFishin.Database.MockData.SeedData.GetCatches(userIds, spotIds);
            await context.Catches.AddRangeAsync(catches);
            await context.SaveChangesAsync();
            logger.LogInformation("Added {Count} catches", catches.Count);

            logger.LogInformation("Seeding completed! TEST ACCOUNTS:");
            logger.LogInformation("  Admin: admin / admin123");
            logger.LogInformation("  Manager: manager1, manager2 / manager123");
            logger.LogInformation("  Users: ion_pescar, maria_fisher, etc. / password123");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while seeding the database.");
        throw;
    }
}

// Configure the HTTP request pipeline
// Swagger is always enabled (accessible at /swagger in Docker too)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "WhereWeFishin API v1");
    c.RoutePrefix = "swagger";
});

// HTTPS redirect only in local dev - in Docker, nginx handles TLS termination
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

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
