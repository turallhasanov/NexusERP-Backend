namespace NexusERP.Domain.Enums;

public enum OtpPurpose
{
    /// <summary>Confirms the e-mail address of a new account.</summary>
    RegisterVerification = 1,

    /// <summary>Proves ownership of the address before setting a new password.</summary>
    PasswordReset = 2
}
