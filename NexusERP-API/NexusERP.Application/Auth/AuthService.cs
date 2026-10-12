using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;

namespace NexusERP.Application.Auth;

public class AuthService(IApplicationDbContext db, IJwtTokenGenerator jwtTokenGenerator) : IAuthService
{
    public const string DemoEmail = "demo@nexuserp.az";
    private const string DemoUserId = "1";

    public async Task<Result<AuthResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim();

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(request.Password))
            return Result<AuthResponseDto>.Failure(ErrorType.Validation, "E-poçt və şifrə tələb olunur.");

        // Only the demo account exists until registration is implemented; its password is not checked.
        if (!string.Equals(email, DemoEmail, StringComparison.OrdinalIgnoreCase))
            return Result<AuthResponseDto>.Failure(ErrorType.Unauthorized, "E-poçt və ya şifrə yanlışdır.");

        var user = await GetOrCreateDemoUserAsync(cancellationToken);

        return new AuthResponseDto(jwtTokenGenerator.GenerateToken(user), UserDto.From(user));
    }

    public async Task<Result<CurrentUserResponseDto>> GetCurrentUserAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.FindAsync([userId], cancellationToken);

        if (user is null)
            return Result<CurrentUserResponseDto>.Failure(ErrorType.NotFound, "İstifadəçi tapılmadı.");

        return new CurrentUserResponseDto(UserDto.From(user));
    }

    private async Task<User> GetOrCreateDemoUserAsync(CancellationToken cancellationToken)
    {
        var user = await db.Users.FindAsync([DemoUserId], cancellationToken);
        if (user is not null)
            return user;

        user = new User
        {
            Id = DemoUserId,
            Name = "Aysel Məmmədova",
            Email = DemoEmail,
            Role = "admin"
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return user;
    }
}
