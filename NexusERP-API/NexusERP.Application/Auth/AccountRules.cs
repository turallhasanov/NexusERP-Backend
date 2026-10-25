using System.Text.RegularExpressions;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Auth;

/// <summary>
/// Account rules shared by sign-up, password reset and the accounts platform administrators create.
/// Validators return an Azerbaijani message, or null when the value is fine.
/// </summary>
public static partial class AccountRules
{
    // Must match the column lengths in UserConfiguration.
    public const int NameMaxLength = 150;
    public const int EmailMaxLength = 150;

    public const int PasswordMinLength = 6;

    // Hashing cost grows with input length; a cap keeps one request from tying up the CPU.
    public const int PasswordMaxLength = 128;

    // Deliberately loose: something@domain.tld with no spaces. Real validation is the code sent to it.
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();

    public static string? NormalizeEmail(string? email) => email?.Trim().ToLowerInvariant();

    public static string? ValidateName(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return "Ad Soyad tələb olunur.";

        return name.Length > NameMaxLength ? $"Ad Soyad {NameMaxLength} simvoldan uzun ola bilməz." : null;
    }

    public static string? ValidateEmail(string? email)
    {
        if (string.IsNullOrEmpty(email))
            return "E-poçt tələb olunur.";

        if (email.Length > EmailMaxLength)
            return $"E-poçt {EmailMaxLength} simvoldan uzun ola bilməz.";

        return EmailPattern().IsMatch(email) ? null : "E-poçt ünvanı düzgün deyil.";
    }

    public static string? ValidatePassword(string? password)
    {
        if (string.IsNullOrEmpty(password))
            return "Şifrə tələb olunur.";

        if (password.Length < PasswordMinLength)
            return $"Şifrə ən azı {PasswordMinLength} simvol olmalıdır.";

        if (password.Length > PasswordMaxLength)
            return $"Şifrə {PasswordMaxLength} simvoldan uzun ola bilməz.";

        return password.Any(char.IsLetter) && password.Any(char.IsDigit)
            ? null
            : "Şifrədə ən azı bir hərf və bir rəqəm olmalıdır.";
    }

    /// <summary>Why the users of a company with this status cannot work, or null when they can.</summary>
    public static string? CompanyBlockedMessage(CompanyStatus status) => status switch
    {
        CompanyStatus.Active => null,
        CompanyStatus.Suspended => "Şirkətinizin hesabı dondurulub. Ətraflı məlumat üçün platforma administratoru ilə əlaqə saxlayın.",
        _ => "Şirkətinizin hesabı passiv vəziyyətdədir. Ətraflı məlumat üçün platforma administratoru ilə əlaqə saxlayın."
    };
}
