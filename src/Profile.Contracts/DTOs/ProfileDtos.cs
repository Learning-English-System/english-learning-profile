namespace Profile.Contracts.DTOs;

public record ProfileDto(
    Guid Id,
    string Email,
    string DisplayName,
    string NativeLanguage,
    string TargetLanguage,
    int DailyGoalMinutes,
    string TargetCefrLevel,
    int DailyReviewTarget,
    bool ReminderEnabled,
    string PreferredDifficulty,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public record UpdateProfileRequest(
    string? DisplayName,
    string? NativeLanguage,
    string? TargetLanguage,
    int? DailyGoalMinutes,
    string? TargetCefrLevel,
    int? DailyReviewTarget,
    bool? ReminderEnabled,
    string? PreferredDifficulty
);
