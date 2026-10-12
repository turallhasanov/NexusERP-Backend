using NexusERP.Application.Common;

namespace NexusERP.Application.Auth;

public interface IAuthService
{
    Task<Result<AuthResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken);
    Task<Result<CurrentUserResponseDto>> GetCurrentUserAsync(string userId, CancellationToken cancellationToken);
}
