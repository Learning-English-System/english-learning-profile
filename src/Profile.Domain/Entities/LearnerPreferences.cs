namespace Profile.Domain.Entities;

public class LearnerPreferences
{
    public Guid UserId { get; set; }
    public int DailyReviewTarget { get; set; } = 20;
    public bool ReminderEnabled { get; set; } = true;
    public string PreferredDifficulty { get; set; } = "Intermediate";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public LearnerProfile Profile { get; set; } = null!;
}
