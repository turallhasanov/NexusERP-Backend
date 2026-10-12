namespace NexusERP.Application.Common.Interfaces;

public enum PasswordCheck
{
    Failed,
    Success,

    /// <summary>Correct, but hashed with older settings; the caller should store a fresh hash.</summary>
    SuccessRehashNeeded
}

/// <summary>Salted, slow password hashing. Never store or compare plain passwords.</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    PasswordCheck Verify(string passwordHash, string password);
}
