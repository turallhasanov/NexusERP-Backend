namespace NexusERP.Application.Products;

/// <summary>The product photos stored on this server (site paths like /uploads/products/{file}).</summary>
public interface IProductImageFiles
{
    /// <summary>Opens a stored photo, or null when the path is not one of ours or the file is gone.</summary>
    Stream? OpenRead(string imageUrl);

    /// <summary>Removes a stored photo (e.g. one that was replaced). Never throws.</summary>
    void TryDelete(string imageUrl);
}
