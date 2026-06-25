using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Trackside.Service.Hosting;

/// <summary>
/// Coordinates the process lifetime for service-style and console-style operation.
/// </summary>
public static class TracksideServiceRunner
{
    /// <summary>
    /// Builds and starts the Trackside backend service.
    /// </summary>
    /// <param name="args">Command-line arguments supplied to the executable.</param>
    /// <returns>Zero when the host exits normally; non-zero when startup throws.</returns>
    public static int Run(string[] args)
    {
        WebApplication? app = null;

        try
        {
            app = TracksideWebApplication.Create(args);
            app.Run();
            return 0;
        }
        catch (Exception ex)
        {
            var logger = app?.Services.GetService<ILoggerFactory>()?.CreateLogger(typeof(TracksideServiceRunner));
            logger?.LogCritical(ex, "Trackside service stopped during startup.");
            return 1;
        }
        finally
        {
            app?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}