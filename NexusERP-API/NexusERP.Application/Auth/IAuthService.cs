using NexusERP.Application.Common;

namespace NexusERP.Application.Auth;

public interface IAuthService
{
    Task<Result<AuthResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken);
    Task<Result<CodeStepResponseDto>> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken);
    Task<Result<AuthResponseDto>> VerifyEmailOtpAsync(VerifyEmailOtpRequestDto request, CancellationToken cancellationToken);
    Task<Result<CodeStepResponseDto>> ResendOtpAsync(EmailRequestDto request, CancellationToken cancellationToken);
    Task<Result<CodeStepResponseDto>> ForgotPasswordAsync(EmailRequestDto request, CancellationToken cancellationToken);
    Task<Result<CodeStepResponseDto>> ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken);
    Task<Result<CurrentUserResponseDto>> GetCurrentUserAsync(string userId, CancellationToken cancellationToken);
}
