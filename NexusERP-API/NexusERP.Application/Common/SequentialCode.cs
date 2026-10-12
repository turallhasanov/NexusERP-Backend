using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Common.Interfaces;

namespace NexusERP.Application.Common;

/// <summary>Per-company sequential codes: DPO-001, DPO-002, ... (past 999 they simply grow: DPO-1000).</summary>
internal static class SequentialCode
{
    private const int MaxSaveAttempts = 3;

    /// <summary>
    /// Assigns the next code and saves. Two concurrent creates can pick the same code; the unique
    /// (CompanyId, Code) index rejects the loser, so the code is recomputed and the save retried.
    /// </summary>
    /// <param name="companyCodes">Codes of the same entity type, already filtered to one company.</param>
    public static async Task AssignAndSaveAsync(
        IApplicationDbContext db,
        IQueryable<string> companyCodes,
        string prefix,
        Action<string> setCode,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            setCode(await NextAsync(companyCodes, prefix, cancellationToken));

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException) when (attempt < MaxSaveAttempts)
            {
                // Most likely a code collision; any other failure repeats and surfaces on the last attempt.
            }
        }
    }

    private static async Task<string> NextAsync(IQueryable<string> companyCodes, string prefix, CancellationToken cancellationToken)
    {
        var codePrefix = prefix + "-";

        // Longest-then-greatest, so "DPO-1000" ranks above "DPO-999".
        var lastCode = await companyCodes
            .Where(code => code.StartsWith(codePrefix))
            .OrderByDescending(code => code.Length)
            .ThenByDescending(code => code)
            .FirstOrDefaultAsync(cancellationToken);

        var lastNumber = lastCode is not null && int.TryParse(lastCode.AsSpan(codePrefix.Length), out var number) ? number : 0;

        return $"{codePrefix}{lastNumber + 1:D3}";
    }
}
