using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.IIS;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Interfaces;
using WhereWeFishin.Core.Services;
using WhereWeFishin.API.Extensions;
using WhereWeFishin.Database.Context;
using WhereWeFishin.Database.Repositories;

var builder = WebApplication.CreateBuilder(args);

var stripeSecretKey = builder.Configuration["Stripe:SecretKey"];
if (!string.IsNullOrWhiteSpace(stripeSecretKey))
{
    Stripe.StripeConfiguration.ApiKey = stripeSecretKey;
}

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 150 * 1024 * 1024; // 150MB
    serverOptions.Limits.MaxConcurrentConnections = 10000;
    serverOptions.Limits.MaxConcurrentUpgradedConnections = 10000;
    serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(100); // Better aligned with Cloudflare timeouts
    serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
    if (!builder.Environment.IsDevelopment())
        serverOptions.ListenAnyIP(8080);
});

builder.Services.AddControllers(options =>
{
    options.MaxModelBindingCollectionSize = 10_000;
})
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
});
builder.Services.AddEndpointsApiExplorer();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 150 * 1024 * 1024;
});

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "WhereWeFishin API", 
        Version = "v1",
        Description = "API for the WhereWeFishin application - managing fishing locations and catches"
    });

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

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
    throw new InvalidOperationException("JWT Key is not configured or is empty. Add a Jwt:Key value in appsettings.Development.json or as an environment variable.");
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

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(builder => builder.Expire(TimeSpan.FromSeconds(30)));
    options.AddPolicy("ShortCache", builder => builder.Expire(TimeSpan.FromMinutes(1)));
    options.AddPolicy("MediumCache", builder => builder.Expire(TimeSpan.FromMinutes(5)));
    options.AddPolicy("LongCache", builder => builder.Expire(TimeSpan.FromMinutes(30)));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", policy =>
    {
        policy.WithOrigins(
                  "http://localhost:4200",      // local Angular dev server
                  "http://localhost",            // Docker: nginx on port 80
                  "http://localhost:80",         // Docker: explicit port
                  "https://wherewefishin.uk",    // Production domain
                  "http://wherewefishin.uk",     // Production domain (http)
                  "https://www.wherewefishin.uk", // Production www subdomain
                  "http://www.wherewefishin.uk"   // Production www subdomain (http)
              )
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()
              .WithExposedHeaders("Content-Length", "Content-Range", "Accept-Ranges");
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy<string>("AuthEndpoints", context =>
    {
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetAuthRateLimitPartitionKey(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });
});

// Register Repositories
builder.Services.AddScoped<IRepository<User>, Repository<User>>();
builder.Services.AddScoped<IRepository<FishingSpot>, FishingSpotRepository>();
builder.Services.AddScoped<IRepository<VideoAnalysis>, Repository<VideoAnalysis>>();
builder.Services.AddScoped<IRepository<FishingSession>, Repository<FishingSession>>();
builder.Services.AddScoped<IRepository<Review>, Repository<Review>>();
builder.Services.AddScoped<IRepository<Pontoon>, Repository<Pontoon>>();
builder.Services.AddScoped<IRepository<SpotEmployee>, Repository<SpotEmployee>>();
builder.Services.AddScoped<IRepository<FishStocking>, Repository<FishStocking>>();
builder.Services.AddScoped<ReviewRepository>();
builder.Services.AddScoped<PontoonRepository>();

// Register Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddHttpClient<IFishRecognitionService, FishRecognitionService>(client =>
{
    var fishServiceUrl = builder.Configuration["FishRecognitionService:Url"] 
        ?? throw new InvalidOperationException("Fish Recognition Service URL not configured");
    client.BaseAddress = new Uri(fishServiceUrl);
    client.Timeout = TimeSpan.FromMinutes(5);
});

// Named HttpClient for fish-recognition auxiliary requests (delete, proxy)
builder.Services.AddHttpClient("FishService", client =>
{
    var fishServiceUrl = builder.Configuration["FishRecognitionService:Url"]
        ?? "http://localhost:5001";
    client.BaseAddress = new Uri(fishServiceUrl);
    client.Timeout = TimeSpan.FromMinutes(5);
});

builder.Services.AddHttpClient();

var app = builder.Build();

app.UseForwardedHeaders();

// Security headers
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    if (!context.Request.IsHttps == false)
    {
        context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    }
    await next();
});

app.UseRateLimiter();

await app.InitializeDatabaseAsync();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "WhereWeFishin API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseResponseCompression();

// HTTPS redirect only in local dev - in Docker, nginx handles TLS termination
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowAngularApp");

app.UseOutputCache();

var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

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
        ctx.Context.Response.Headers["Accept-Ranges"] = "bytes";
        
        ctx.Context.Response.Headers["Cache-Control"] = "private, max-age=3600";
    }
});

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok("OK"));

app.MapGet("/health/ready", async (ApplicationDbContext db) =>
{
    try
    {
        await db.Database.ExecuteSqlRawAsync("SELECT 1");
        return Results.Ok(new { status = "healthy", database = "connected" });
    }
    catch (Exception ex)
    {
        return Results.Json(
            new { status = "unhealthy", database = "disconnected", error = ex.Message },
            statusCode: 503);
    }
});

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    var isVideoEndpoint = path.Contains("/videoanalysis/", StringComparison.OrdinalIgnoreCase)
                       || path.Contains("/processed-video/", StringComparison.OrdinalIgnoreCase);
    var timeout = isVideoEndpoint ? TimeSpan.FromMinutes(20) : TimeSpan.FromSeconds(120);

    using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
    cts.CancelAfter(timeout);
    context.RequestAborted = cts.Token;
    await next();
});

app.MapControllers();

app.Run();

static string GetAuthRateLimitPartitionKey(HttpContext context)
{
    var remoteIp = context.Connection.RemoteIpAddress;
    if (remoteIp == null)
    {
        return "unknown";
    }

    if (remoteIp.IsIPv4MappedToIPv6)
    {
        remoteIp = remoteIp.MapToIPv4();
    }

    return remoteIp.ToString();
}
