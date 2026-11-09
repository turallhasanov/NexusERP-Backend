using Serilog;

namespace NexusERP.Api.Logging;

/// <summary>
/// Builds the Serilog logger from the "Serilog" section of appsettings.json (levels, console, daily file). Every
/// <c>ILogger&lt;T&gt;</c> in the app writes through it once it is registered with <c>AddSerilog</c>.
/// </summary>
public static class SerilogSetup
{
    /// <summary>Logger for the moments before the configuration is read: a startup failure still reaches the console.</summary>
    public static Serilog.ILogger CreateBootstrapLogger() =>
        new LoggerConfiguration().WriteTo.Console().CreateLogger();

    public static Serilog.ILogger CreateLogger(IConfiguration configuration, IHostEnvironment environment)
    {
        AnchorFilePaths(configuration, environment.ContentRootPath);

        return new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();
    }

    /// <summary>
    /// Serilog resolves a relative file path ("Logs/log-.txt") against the process's working directory, which is the
    /// project folder under Visual Studio but the solution folder for <c>dotnet run --project</c>. Anchoring it to the
    /// content root keeps the logs in NexusERP.Api/Logs however the API is started. Absolute paths are left alone.
    /// </summary>
    private static void AnchorFilePaths(IConfiguration configuration, string contentRoot) =>
        AnchorSinks(configuration.GetSection("Serilog:WriteTo"), contentRoot);

    // The file sink sits inside the Async wrapper ("Args:configure"), so the search follows wrappers down.
    private static void AnchorSinks(IConfigurationSection sinks, string contentRoot)
    {
        foreach (var sink in sinks.GetChildren())
        {
            if (string.Equals(sink["Name"], "Async", StringComparison.OrdinalIgnoreCase))
            {
                AnchorSinks(sink.GetSection("Args:configure"), contentRoot);
                continue;
            }

            if (!string.Equals(sink["Name"], "File", StringComparison.OrdinalIgnoreCase))
                continue;

            var path = sink["Args:path"];
            if (!string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path))
                sink["Args:path"] = Path.Combine(contentRoot, path);
        }
    }
}
