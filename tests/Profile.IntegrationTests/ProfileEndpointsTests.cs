using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using BuildingBlocks.Messaging.Contracts;
using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Profile.Application.Services;
using Profile.Contracts.DTOs;
using Profile.Infrastructure.Consumers;
using Profile.Infrastructure.Data;
using Xunit;

namespace Profile.IntegrationTests;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string AuthenticationScheme = "TestScheme";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? authHeader = Request.Headers.Authorization;
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string userId = authHeader["Bearer ".Length..].Trim();
        if (Guid.TryParse(userId, out var guid))
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, guid.ToString()),
                new Claim("sub", guid.ToString()),
                new Claim(ClaimTypes.Role, "Learner"),
                new Claim("scope", "openid profile email")
            };
            var identity = new ClaimsIdentity(claims, AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, AuthenticationScheme)));
        }

        return Task.FromResult(AuthenticateResult.Fail("Invalid test bearer token"));
    }
}

public class ProfileTestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.AuthenticationScheme;
                options.DefaultChallengeScheme = TestAuthHandler.AuthenticationScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.AuthenticationScheme, null);
        });
    }
}

public class ProfileEndpointsTests : IClassFixture<ProfileTestWebApplicationFactory>
{
    private readonly ProfileTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ProfileEndpointsTests(ProfileTestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetMyProfile_Unauthenticated_ReturnsUnauthorized()
    {
        // Act: Request without authorization header
        var response = await _client.GetAsync("/api/profile/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EventConsumption_UserRegistered_CreatesDefaultProfileShellIdempotently()
    {
        // Arrange
        var userId = Guid.NewGuid();
        string email = $"registered_{userId:N}@test.com";
        string displayName = "Initial Learner";

        var domainEvent = new UserRegisteredIntegrationPayload(userId, email, displayName);
        var envelope = IntegrationEventEnvelope<UserRegisteredIntegrationPayload>.Create(
            domainEvent.EventType, domainEvent.EventVersion, "auth-service", "cid-test", domainEvent);
        string rawJson = JsonSerializerHelper.Serialize(envelope);

        using (var scope = _factory.Services.CreateScope())
        {
            var processor = scope.ServiceProvider.GetRequiredService<IIdempotentEventProcessor>();
            var profileService = scope.ServiceProvider.GetRequiredService<IProfileService>();

            // Act 1: First event consumption
            bool processed1 = await processor.ProcessEventAsync<UserRegisteredIntegrationPayload>(
                "profile-service-user-registered",
                rawJson,
                async (env, ct) =>
                {
                    await profileService.CreateInitialProfileAsync(
                        env.Payload.UserId, env.Payload.Email, env.Payload.DisplayName, ct);
                });

            processed1.Should().BeTrue();

            // Act 2: Duplicate delivery of same event
            bool processed2 = await processor.ProcessEventAsync<UserRegisteredIntegrationPayload>(
                "profile-service-user-registered",
                rawJson,
                async (env, ct) =>
                {
                    await profileService.CreateInitialProfileAsync(
                        env.Payload.UserId, env.Payload.Email, env.Payload.DisplayName, ct);
                });

            processed2.Should().BeTrue("Duplicate delivery should be gracefully ignored");
        }

        // Assert: Exactly one profile created with default settings
        using (var verifyScope = _factory.Services.CreateScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<ProfileDbContext>();
            var profiles = await db.Profiles.Include(p => p.Preferences).Where(p => p.Id == userId).ToListAsync();

            profiles.Should().HaveCount(1);
            var profile = profiles.First();
            profile.Email.Should().Be(email);
            profile.DisplayName.Should().Be(displayName);
            profile.NativeLanguage.Should().Be("vi");
            profile.TargetLanguage.Should().Be("en");
            profile.DailyGoalMinutes.Should().Be(15);
            profile.TargetCefrLevel.Should().Be("B2");

            profile.Preferences.Should().NotBeNull();
            profile.Preferences!.DailyReviewTarget.Should().Be(20);
            profile.Preferences.ReminderEnabled.Should().BeTrue();
            profile.Preferences.PreferredDifficulty.Should().Be("Intermediate");
        }
    }

    [Fact]
    public async Task GetMyProfile_Authenticated_ReturnsProfileData()
    {
        // Arrange: Create profile directly
        var userId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var profileService = scope.ServiceProvider.GetRequiredService<IProfileService>();
            await profileService.CreateInitialProfileAsync(userId, $"test_{userId:N}@test.com", "Profile Reader");
        }

        using var authedClient = _factory.CreateClient();
        authedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userId.ToString());

        // Act
        var response = await authedClient.GetAsync("/api/profile/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ProfileDto>();
        dto.Should().NotBeNull();
        dto!.Id.Should().Be(userId);
        dto.DisplayName.Should().Be("Profile Reader");
        dto.DailyGoalMinutes.Should().Be(15);
    }

    [Fact]
    public async Task UpdateMyProfile_Authenticated_UpdatesFieldsAndPreferences()
    {
        // Arrange
        var userId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var profileService = scope.ServiceProvider.GetRequiredService<IProfileService>();
            await profileService.CreateInitialProfileAsync(userId, $"update_{userId:N}@test.com", "Original Name");
        }

        using var authedClient = _factory.CreateClient();
        authedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userId.ToString());

        var updateRequest = new UpdateProfileRequest(
            DisplayName: "Updated Advanced Learner",
            NativeLanguage: "vi",
            TargetLanguage: "en",
            DailyGoalMinutes: 30,
            TargetCefrLevel: "C1",
            DailyReviewTarget: 50,
            ReminderEnabled: false,
            PreferredDifficulty: "Advanced"
        );

        // Act
        var response = await authedClient.PatchAsJsonAsync("/api/profile/me", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ProfileDto>();
        updated.Should().NotBeNull();
        updated!.DisplayName.Should().Be("Updated Advanced Learner");
        updated.DailyGoalMinutes.Should().Be(30);
        updated.TargetCefrLevel.Should().Be("C1");
        updated.DailyReviewTarget.Should().Be(50);
        updated.ReminderEnabled.Should().BeFalse();
        updated.PreferredDifficulty.Should().Be("Advanced");

        // Verify in DB
        using (var verifyScope = _factory.Services.CreateScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<ProfileDbContext>();
            var profile = await db.Profiles.Include(p => p.Preferences).FirstOrDefaultAsync(p => p.Id == userId);
            profile.Should().NotBeNull();
            profile!.DisplayName.Should().Be("Updated Advanced Learner");
            profile.TargetCefrLevel.Should().Be("C1");
            profile.Preferences!.DailyReviewTarget.Should().Be(50);
            profile.Preferences.ReminderEnabled.Should().BeFalse();
        }
    }
}
