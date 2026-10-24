using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Auth;

public partial class AuthService(
    IApplicationDbContext db,
    IJwtTokenGenerator jwtTokenGenerator,
    IPasswordHasher passwordHasher,
    IOtpVerificationService otpService) : IAuthService
{
    public const string DemoEmail = "demo@nexuserp.az";
    private const string DemoUserId = "1";

    private const string InvalidCredentials = "E-poçt və ya şifrə yanlışdır.";
    private const string EmailTaken = "Bu e-poçt ünvanı artıq qeydiyyatdan keçib.";
    private const string AccountDisabled = "Hesabınız deaktiv edilib.";
    private const string EmailNotConfirmed = "E-poçt ünvanınız təsdiqlənməyib. E-poçtunuza göndərilən kodu daxil edin.";
    private const string AlreadyConfirmed = "E-poçt ünvanınız artıq təsdiqlənib. Daxil ola bilərsiniz.";
    private const string CodeSent = "Təsdiq kodu e-poçt ünvanınıza göndərildi.";
    private const string SendFailed = "E-poçt göndərilə bilmədi. Bir az sonra yenidən cəhd edin.";

    [GeneratedRegex(@"^\d{6}$")]
    private static partial Regex CodePattern();

    public async Task<Result<AuthResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(request.Password))
            return Result<AuthResponseDto>.Failure(ErrorType.Validation, "E-poçt və şifrə tələb olunur.");

        var user = email == DemoEmail
            ? await GetOrCreateDemoUserAsync(cancellationToken)
            : await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        // Unknown email and wrong password get the same answer, so the form cannot be used to find accounts.
        if (user is null)
            return Result<AuthResponseDto>.Failure(ErrorType.Unauthorized, InvalidCredentials);

        // The demo account has no password and accepts any; every registered account has a hash.
        if (user.PasswordHash.Length > 0)
        {
            var check = passwordHasher.Verify(user.PasswordHash, request.Password);

            if (check == PasswordCheck.Failed)
                return Result<AuthResponseDto>.Failure(ErrorType.Unauthorized, InvalidCredentials);

            if (check == PasswordCheck.SuccessRehashNeeded)
            {
                user.PasswordHash = passwordHasher.Hash(request.Password);
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        // Checked after the password, so only the account owner learns the account is disabled or unconfirmed.
        if (!user.IsActive)
            return Result<AuthResponseDto>.Failure(ErrorType.Unauthorized, AccountDisabled);

        // 403 tells the client to open the code screen for this address.
        if (!user.IsEmailConfirmed)
            return Result<AuthResponseDto>.Failure(ErrorType.Forbidden, EmailNotConfirmed);

        if (await CompanyBlockedMessageAsync(user, cancellationToken) is { } blocked)
            return Result<AuthResponseDto>.Failure(ErrorType.Unauthorized, blocked);

        return new AuthResponseDto(jwtTokenGenerator.GenerateToken(user), UserDto.From(user));
    }

    /// <summary>Creates an unconfirmed account and e-mails a 6-digit code; no token until the code is entered.</summary>
    public async Task<Result<CodeStepResponseDto>> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken)
    {
        var name = request.FullName?.Trim();
        var email = NormalizeEmail(request.Email);

        if (AccountRules.ValidateName(name) is { } nameError)
            return Result<CodeStepResponseDto>.Failure(ErrorType.Validation, nameError);

        if (ValidateEmail(email) is { } emailError)
            return Result<CodeStepResponseDto>.Failure(ErrorType.Validation, emailError);

        if (ValidatePassword(request.Password) is { } passwordError)
            return Result<CodeStepResponseDto>.Failure(ErrorType.Validation, passwordError);

        // Taken even when unconfirmed: the real owner of an address someone else registered recovers it
        // through "forgot password", which also confirms the e-mail.
        // The demo address is reserved even before the demo account is first created.
        if (email == DemoEmail || await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
            return Result<CodeStepResponseDto>.Failure(ErrorType.Conflict, EmailTaken);

        var user = new User
        {
            Name = name!,
            Email = email!,
            PasswordHash = passwordHasher.Hash(request.Password!),
            // Someone who signs up is about to register a company, and its founder is the IT admin.
            // Joining with an invite code replaces this with staff.
            Role = AppRoles.ITAdmin,
            IsEmailConfirmed = false
        };

        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two sign-ups with the same address at once: the unique index on Email lets only one through.
            if (await db.Users.AsNoTracking().AnyAsync(u => u.Email == email, cancellationToken))
                return Result<CodeStepResponseDto>.Failure(ErrorType.Conflict, EmailTaken);

            throw;
        }

        var issued = await otpService.IssueAsync(user.Email, OtpPurpose.RegisterVerification, cancellationToken);

        if (issued.Status == OtpIssueStatus.SendFailed)
        {
            // Without the code the account could only be recovered via password reset; undo it so the
            // user can simply register again once mail works.
            db.Users.Remove(user);
            await db.SaveChangesAsync(cancellationToken);

            return Result<CodeStepResponseDto>.Failure(ErrorType.ServiceUnavailable, SendFailed);
        }

        return new CodeStepResponseDto(user.Email, CodeSent, issued.RetryAfterSeconds);
    }

    /// <summary>Confirms the e-mail with the registration code and logs the user in.</summary>
    public async Task<Result<AuthResponseDto>> VerifyEmailOtpAsync(VerifyEmailOtpRequestDto request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var code = request.Code?.Trim();

        if (string.IsNullOrEmpty(email))
            return Result<AuthResponseDto>.Failure(ErrorType.Validation, "E-poçt tələb olunur.");

        if (ValidateCode(code) is { } codeError)
            return Result<AuthResponseDto>.Failure(ErrorType.Validation, codeError);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is { IsEmailConfirmed: true })
            return Result<AuthResponseDto>.Failure(ErrorType.Conflict, AlreadyConfirmed);

        // An unknown address has no codes, so it fails exactly like a wrong or expired code.
        var check = await otpService.ConsumeAsync(email, OtpPurpose.RegisterVerification, code!, cancellationToken);

        if (CodeError(check) is { } checkError)
            return Result<AuthResponseDto>.Failure(ErrorType.Validation, checkError);

        if (user is null)
            return Result<AuthResponseDto>.Failure(ErrorType.Validation, CodeError(OtpCheckResult.ExpiredOrMissing)!);

        user.IsEmailConfirmed = true;
        await db.SaveChangesAsync(cancellationToken);

        if (!user.IsActive)
            return Result<AuthResponseDto>.Failure(ErrorType.Unauthorized, AccountDisabled);

        if (await CompanyBlockedMessageAsync(user, cancellationToken) is { } blocked)
            return Result<AuthResponseDto>.Failure(ErrorType.Unauthorized, blocked);

        return new AuthResponseDto(jwtTokenGenerator.GenerateToken(user), UserDto.From(user));
    }

    /// <summary>Sends a new registration code (at most once a minute).</summary>
    public async Task<Result<CodeStepResponseDto>> ResendOtpAsync(EmailRequestDto request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);

        if (ValidateEmail(email) is { } emailError)
            return Result<CodeStepResponseDto>.Failure(ErrorType.Validation, emailError);

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is { IsEmailConfirmed: true })
            return Result<CodeStepResponseDto>.Failure(ErrorType.Conflict, AlreadyConfirmed);

        // No account: answer as if sent, without sending anything.
        if (user is null)
            return new CodeStepResponseDto(email!, CodeSent, (int)OtpVerificationService.ResendCooldown.TotalSeconds);

        return IssuedResponse(email!, CodeSent, await otpService.IssueAsync(email!, OtpPurpose.RegisterVerification, cancellationToken));
    }

    /// <summary>
    /// E-mails a password reset code if the address has an active account. The answer is the same either
    /// way, so the form cannot be used to find out which addresses are registered.
    /// </summary>
    public async Task<Result<CodeStepResponseDto>> ForgotPasswordAsync(EmailRequestDto request, CancellationToken cancellationToken)
    {
        const string message = "Bu e-poçt ünvanı ilə hesab varsa, şifrə bərpası kodu göndərildi.";
        var email = NormalizeEmail(request.Email);

        if (ValidateEmail(email) is { } emailError)
            return Result<CodeStepResponseDto>.Failure(ErrorType.Validation, emailError);

        var cooldown = (int)OtpVerificationService.ResendCooldown.TotalSeconds;
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        // The demo account has no password to reset.
        if (user is not { IsActive: true } || user.PasswordHash.Length == 0)
            return new CodeStepResponseDto(email!, message, cooldown);

        var issued = await otpService.IssueAsync(email!, OtpPurpose.PasswordReset, cancellationToken);

        // A cooldown (or its remaining seconds) would reveal that the account exists, so every answer looks
        // like a fresh send to an unknown address.
        return issued.Status switch
        {
            OtpIssueStatus.SendFailed => Result<CodeStepResponseDto>.Failure(ErrorType.ServiceUnavailable, SendFailed),
            _ => new CodeStepResponseDto(email!, message, cooldown)
        };
    }

    /// <summary>Sets a new password with the reset code. Proves ownership of the address, so it also confirms it.</summary>
    public async Task<Result<CodeStepResponseDto>> ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var code = request.Code?.Trim();

        if (string.IsNullOrEmpty(email))
            return Result<CodeStepResponseDto>.Failure(ErrorType.Validation, "E-poçt tələb olunur.");

        if (ValidateCode(code) is { } codeError)
            return Result<CodeStepResponseDto>.Failure(ErrorType.Validation, codeError);

        // Checked before the code, so a weak password does not use up the code.
        if (ValidatePassword(request.NewPassword) is { } passwordError)
            return Result<CodeStepResponseDto>.Failure(ErrorType.Validation, passwordError);

        var check = await otpService.ConsumeAsync(email, OtpPurpose.PasswordReset, code!, cancellationToken);

        if (CodeError(check) is { } checkError)
            return Result<CodeStepResponseDto>.Failure(ErrorType.Validation, checkError);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null)
            return Result<CodeStepResponseDto>.Failure(ErrorType.Validation, CodeError(OtpCheckResult.ExpiredOrMissing)!);

        user.PasswordHash = passwordHasher.Hash(request.NewPassword!);
        user.IsEmailConfirmed = true;
        await db.SaveChangesAsync(cancellationToken);

        return new CodeStepResponseDto(user.Email, "Şifrəniz yeniləndi. Yeni şifrə ilə daxil olun.", 0);
    }

    public async Task<Result<CurrentUserResponseDto>> GetCurrentUserAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.FindAsync([userId], cancellationToken);

        if (user is null)
            return Result<CurrentUserResponseDto>.Failure(ErrorType.NotFound, "İstifadəçi tapılmadı.");

        return new CurrentUserResponseDto(UserDto.From(user));
    }

    private static Result<CodeStepResponseDto> IssuedResponse(string email, string message, OtpIssueResult issued) =>
        issued.Status switch
        {
            OtpIssueStatus.Cooldown => Result<CodeStepResponseDto>.Failure(
                ErrorType.TooManyRequests, $"Yeni kod üçün {issued.RetryAfterSeconds} saniyə gözləyin."),
            OtpIssueStatus.SendFailed => Result<CodeStepResponseDto>.Failure(ErrorType.ServiceUnavailable, SendFailed),
            _ => new CodeStepResponseDto(email, message, issued.RetryAfterSeconds)
        };

    private static string? CodeError(OtpCheckResult check) => check switch
    {
        OtpCheckResult.Valid => null,
        OtpCheckResult.Invalid => "Kod yanlışdır.",
        OtpCheckResult.TooManyAttempts => "Çox sayda yanlış cəhd edildi. Yeni kod tələb edin.",
        _ => "Kodun vaxtı bitib və ya etibarsızdır. Yeni kod tələb edin."
    };

    private static string? ValidateEmail(string? email) => AccountRules.ValidateEmail(email);

    private static string? ValidatePassword(string? password) => AccountRules.ValidatePassword(password);

    private static string? ValidateCode(string? code)
    {
        if (string.IsNullOrEmpty(code))
            return "Təsdiq kodu tələb olunur.";

        return CodePattern().IsMatch(code) ? null : "Kod 6 rəqəmdən ibarət olmalıdır.";
    }

    private static string? NormalizeEmail(string? email) => AccountRules.NormalizeEmail(email);

    /// <summary>Users of a passive or suspended company get no token. Platform admins belong to no company.</summary>
    private async Task<string?> CompanyBlockedMessageAsync(User user, CancellationToken cancellationToken)
    {
        if (user.CompanyId is null)
            return null;

        var status = await db.Companies
            .AsNoTracking()
            .Where(c => c.Id == user.CompanyId)
            .Select(c => (CompanyStatus?)c.Status)
            .FirstOrDefaultAsync(cancellationToken);

        return status is { } current ? AccountRules.CompanyBlockedMessage(current) : null;
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
            Role = AppRoles.Admin,
            IsEmailConfirmed = true
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return user;
    }
}
