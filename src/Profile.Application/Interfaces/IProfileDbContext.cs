using BuildingBlocks.Messaging.Inbox;
using Microsoft.EntityFrameworkCore;
using Profile.Domain.Entities;

namespace Profile.Application.Interfaces;

public interface IProfileDbContext
{
    DbSet<LearnerProfile> Profiles { get; }
    DbSet<LearnerPreferences> Preferences { get; }
    DbSet<InboxMessage> InboxMessages { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
