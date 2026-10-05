namespace Profile.Domain.Entities;

public class LearnerProfile
{
    public Guid Id { get; set; } // Matches Auth UserId
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string NativeLanguage { get; set; } = "vi";
    public string TargetLanguage { get; set; } = "en";
    public int DailyGoalMinutes { get; set; } = 15;
    public string TargetCefrLevel { get; set; } = "B2";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public LearnerPreferences? Preferences { get; set; }
}
