using BuildingBlocks.Web.Exceptions;
using Microsoft.EntityFrameworkCore;
using Profile.Application.Interfaces;
using Profile.Contracts.DTOs;
using Profile.Domain.Entities;

namespace Profile.Application.Services;

public class ProfileService : IProfileService
{
    private readonly IProfileDbContext _dbContext;

    public ProfileService(IProfileDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProfileDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await _dbContext.Profiles
            .Include(p => p.Preferences)
            .FirstOrDefaultAsync(p => p.Id == userId, cancellationToken);

        if (profile == null)
        {
            throw new NotFoundException("Profile", userId);
        }

        return MapToDto(profile);
    }

    public async Task<ProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var profile = await _dbContext.Profiles
            .Include(p => p.Preferences)
            .FirstOrDefaultAsync(p => p.Id == userId, cancellationToken);

        if (profile == null)
        {
            throw new NotFoundException("Profile", userId);
        }

        if (!string.IsNullOrWhiteSpace(request.DisplayName))
        {
            profile.DisplayName = request.DisplayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.NativeLanguage))
        {
            profile.NativeLanguage = request.NativeLanguage.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.TargetLanguage))
        {
            profile.TargetLanguage = request.TargetLanguage.Trim();
        }

        if (request.DailyGoalMinutes.HasValue && request.DailyGoalMinutes.Value > 0)
        {
            profile.DailyGoalMinutes = request.DailyGoalMinutes.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.TargetCefrLevel))
        {
            profile.TargetCefrLevel = request.TargetCefrLevel.Trim().ToUpperInvariant();
        }

        profile.UpdatedAt = DateTimeOffset.UtcNow;

        if (profile.Preferences != null)
        {
            if (request.DailyReviewTarget.HasValue && request.DailyReviewTarget.Value > 0)
            {
                profile.Preferences.DailyReviewTarget = request.DailyReviewTarget.Value;
            }

            if (request.ReminderEnabled.HasValue)
            {
                profile.Preferences.ReminderEnabled = request.ReminderEnabled.Value;
            }

            if (!string.IsNullOrWhiteSpace(request.PreferredDifficulty))
            {
                profile.Preferences.PreferredDifficulty = request.PreferredDifficulty.Trim();
            }

            profile.Preferences.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(profile);
    }

    public async Task<ProfileDto> CreateInitialProfileAsync(Guid userId, string email, string displayName, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.Profiles
            .Include(p => p.Preferences)
            .FirstOrDefaultAsync(p => p.Id == userId, cancellationToken);

        if (existing != null)
        {
            return MapToDto(existing);
        }

        var profile = new LearnerProfile
        {
            Id = userId,
            Email = email.Trim().ToLowerInvariant(),
            DisplayName = !string.IsNullOrWhiteSpace(displayName) ? displayName.Trim() : email.Split('@')[0],
            NativeLanguage = "vi",
            TargetLanguage = "en",
            DailyGoalMinutes = 15,
            TargetCefrLevel = "B2",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Preferences = new LearnerPreferences
            {
                UserId = userId,
                DailyReviewTarget = 20,
                ReminderEnabled = true,
                PreferredDifficulty = "Intermediate",
                UpdatedAt = DateTimeOffset.UtcNow
            }
        };

        await _dbContext.Profiles.AddAsync(profile, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(profile);
    }

    private static ProfileDto MapToDto(LearnerProfile profile)
    {
        var prefs = profile.Preferences;
        return new ProfileDto(
            Id: profile.Id,
            Email: profile.Email,
            DisplayName: profile.DisplayName,
            NativeLanguage: profile.NativeLanguage,
            TargetLanguage: profile.TargetLanguage,
            DailyGoalMinutes: profile.DailyGoalMinutes,
            TargetCefrLevel: profile.TargetCefrLevel,
            DailyReviewTarget: prefs?.DailyReviewTarget ?? 20,
            ReminderEnabled: prefs?.ReminderEnabled ?? true,
            PreferredDifficulty: prefs?.PreferredDifficulty ?? "Intermediate",
            CreatedAt: profile.CreatedAt,
            UpdatedAt: profile.UpdatedAt
        );
    }
}
