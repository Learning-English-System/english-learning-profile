using Profile.Contracts.DTOs;

namespace Profile.Application.Services;

public interface IProfileService
{
    Task<ProfileDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default);
    Task<ProfileDto> CreateInitialProfileAsync(Guid userId, string email, string displayName, CancellationToken cancellationToken = default);
}
