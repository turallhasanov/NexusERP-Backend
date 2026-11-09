using System.Text.RegularExpressions;

namespace NexusERP.Application.Products;

/// <summary>
/// NexusERP has no variant entity: a size or colour of a product is its own product named "Name (Size)", "Name (Color)" or
/// "Name (Color / Size)", and the Khazar store reads the trailing group as the variant's details. Copying an existing
/// variant and adding another size gives "Usaq Hoodie (S) (M)", which the store would read as a different product
/// "Usaq Hoodie (S)" with size M. This keeps only the last group, so the name stays "Usaq Hoodie (M)".
/// </summary>
public static partial class VariantName
{
    /// <summary>
    /// Collapses stacked variant groups ("Name (S) (M)" → "Name (M)"). Groups that do not read like a size or a colour,
    /// such as "(2-3 yaş)", are left alone.
    /// </summary>
    public static string Normalize(string name)
    {
        var current = name.Trim();

        while (StackedGroups().Match(current) is { Success: true } match
               && LooksLikeVariant(match.Groups["first"].Value)
               && LooksLikeVariant(match.Groups["last"].Value))
        {
            current = $"{match.Groups["base"].Value.Trim()} ({match.Groups["last"].Value.Trim()})";
        }

        return current;
    }

    // "S", "Qara", or "Qara / M": the shapes the store accepts for a variant (same rules as its name parser).
    private static bool LooksLikeVariant(string inner)
    {
        var parts = inner.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        return parts.Length switch
        {
            1 => SizePattern().IsMatch(parts[0]) || ColorPattern().IsMatch(parts[0]),
            2 => true,
            _ => false,
        };
    }

    [GeneratedRegex(@"^(?<base>.+?)\s*\((?<first>[^()]*)\)\s*\((?<last>[^()]*)\)$")]
    private static partial Regex StackedGroups();

    [GeneratedRegex(@"^(?:[2-6]?XS|[2-6]?XL|XXL|XXXL|S|M|L|\d{1,3}(?:[.,]5)?)$", RegexOptions.IgnoreCase)]
    private static partial Regex SizePattern();

    [GeneratedRegex(@"^\p{L}[\p{L} ]*$")]
    private static partial Regex ColorPattern();
}
