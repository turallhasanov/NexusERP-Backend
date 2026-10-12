namespace NexusERP.Application.Products;

/// <summary>
/// EAN-13 barcodes for products that arrive without one. The "20" prefix is the GS1 restricted-circulation
/// range (reserved for in-store use, never assigned to manufacturers), so these cannot clash with real
/// packaging barcodes. Layout: "20" + 10 random digits + check digit.
/// </summary>
internal static class InStoreBarcode
{
    public static string Generate()
    {
        Span<char> digits = stackalloc char[13];
        digits[0] = '2';
        digits[1] = '0';

        for (var i = 2; i < 12; i++)
            digits[i] = (char)('0' + Random.Shared.Next(10));

        digits[12] = CheckDigit(digits[..12]);

        return new string(digits);
    }

    /// <summary>EAN-13 check digit: weights 1,3,1,3,... from the left over the first 12 digits.</summary>
    private static char CheckDigit(ReadOnlySpan<char> first12)
    {
        var sum = 0;

        for (var i = 0; i < first12.Length; i++)
            sum += (first12[i] - '0') * (i % 2 == 0 ? 1 : 3);

        return (char)('0' + (10 - sum % 10) % 10);
    }
}
