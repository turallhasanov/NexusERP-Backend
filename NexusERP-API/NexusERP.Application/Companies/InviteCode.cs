using System.Security.Cryptography;

namespace NexusERP.Application.Companies;

/// <summary>
/// Company invite codes: 8 characters from an alphabet without look-alikes (no 0/O, 1/I/L), so they can be
/// read out or typed from a message without mistakes. 31^8 ≈ 8.5 × 10^11 combinations.
/// </summary>
internal static class InviteCode
{
    public const int Length = 8;
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static string Generate() =>
        string.Create(Length, 0, (chars, _) =>
        {
            for (var i = 0; i < chars.Length; i++)
                chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        });

    /// <summary>What users type: spaces and dashes removed, case ignored.</summary>
    public static string Normalize(string code) =>
        new(code.Where(c => !char.IsWhiteSpace(c) && c != '-').Select(char.ToUpperInvariant).ToArray());
}
