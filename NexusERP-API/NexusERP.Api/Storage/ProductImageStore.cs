using System.Text.RegularExpressions;
using NexusERP.Application.Products;

namespace NexusERP.Api.Storage;

/// <summary>
/// Product photos on disk under wwwroot/uploads/products, served at /uploads/products/{file}. The type is read from the
/// file's own content (JPEG, PNG or WebP), never from the client's file name or content type, and the stored name is
/// generated, so an upload cannot choose where it lands or what it is called.
/// </summary>
public sealed partial class ProductImageStore : IProductImageFiles
{
    public const long MaxFileBytes = 8 * 1024 * 1024;
    public const string RequestPath = "/uploads";

    private const string Folder = "products";
    private const int HeaderBytes = 12;

    private readonly string _root;

    public ProductImageStore(IWebHostEnvironment environment)
    {
        // Own folder next to the content root: the API has no wwwroot of its own.
        _root = Path.Combine(environment.ContentRootPath, "wwwroot", "uploads");
        Directory.CreateDirectory(Path.Combine(_root, Folder));
    }

    /// <summary>Physical folder behind <see cref="RequestPath"/>.</summary>
    public string UploadsRoot => _root;

    /// <summary>Stores the image and returns its site path; null with <paramref name="error"/> set when it is refused.</summary>
    public async Task<string?> SaveAsync(IFormFile file, Action<string> error, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            error("Şəkil faylı boşdur.");
            return null;
        }

        if (file.Length > MaxFileBytes)
        {
            error("Şəkil ən çox 8 MB ola bilər.");
            return null;
        }

        await using var input = file.OpenReadStream();
        var header = new byte[HeaderBytes];
        var read = await input.ReadAtLeastAsync(header, HeaderBytes, throwOnEndOfStream: false, cancellationToken);
        var extension = DetectExtension(header.AsSpan(0, read));

        if (extension is null)
        {
            error("Yalnız JPEG, PNG və WebP şəkilləri qəbul olunur.");
            return null;
        }

        input.Position = 0;
        var fileName = $"{Guid.NewGuid():N}{extension}";

        await using (var output = File.Create(Path.Combine(_root, Folder, fileName)))
            await input.CopyToAsync(output, cancellationToken);

        return $"{RequestPath}/{Folder}/{fileName}";
    }

    public Stream? OpenRead(string imageUrl)
    {
        var path = ResolvePath(imageUrl);
        return path is not null && File.Exists(path) ? File.OpenRead(path) : null;
    }

    public void TryDelete(string imageUrl)
    {
        try
        {
            if (ResolvePath(imageUrl) is { } path)
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover file is harmless.
        }
    }

    // Only paths this store generated resolve to a file, whatever string a caller passes.
    private string? ResolvePath(string imageUrl) =>
        StoredPath().IsMatch(imageUrl)
            ? Path.Combine(_root, Folder, imageUrl[(RequestPath.Length + 1 + Folder.Length + 1)..])
            : null;

    [GeneratedRegex(@"^/uploads/products/[0-9a-f]{32}\.(?:jpg|png|webp)$")]
    private static partial Regex StoredPath();

    private static string? DetectExtension(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith<byte>([0xFF, 0xD8, 0xFF]))
            return ".jpg";
        if (header.StartsWith<byte>([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
            return ".png";
        if (header.Length >= HeaderBytes && header[..4].SequenceEqual("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WEBP"u8))
            return ".webp";
        return null;
    }
}
