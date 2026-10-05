using BuildingBlocks.Observability;
using BuildingBlocks.Web.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Profile.Application.Extensions;
using Profile.Infrastructure.Data;
using Profile.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Logging
builder.Logging.AddCustomStructuredLogging(builder.Environment);

// Core services
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Shared building blocks
builder.Services.AddBuildingBlocksWeb();

// Profile domain layers
builder.Services.AddProfileInfrastructure(builder.Configuration);
builder.Services.AddProfileApplication();

// Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["AUTH_AUTHORITY"] ?? "http://localhost:5001";
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true
        };
    });

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:8080")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Run DB migrations
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ProfileDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (args.Contains("--migrate"))
{
    app.Logger.LogInformation("Profile database migration complete. Exiting (--migrate flag present).");
    return;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseBuildingBlocksWeb();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }
