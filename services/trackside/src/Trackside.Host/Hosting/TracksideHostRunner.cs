using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Trackside.Host.Configuration;
using Trackside.Host.Tray;
using System.Windows.Forms;

namespace Trackside.Host.Hosting;

/// <summary>
/// Coordinates the process lifetime for console-style and tray-style operation.
/// </summary>
public static class TracksideHostRunner
{
    /// <summary>
    /// Builds and starts the Trackside web application using the configured lifetime mode.
    /// </summary>
    /// <param name="args">Command-line arguments supplied to the executable.</param>
    /// <returns>Zero when the host exits normally; non-zero when startup throws.</returns>
    public static int Run(string[] args)
    {
        WebApplication? app = null;

        try
        {
            app = TracksideWebApplication.Create(args);
            var options = app.Services.GetRequiredService<IOptions<TracksideOptions>>().Value;

            if (options.Tray.Enabled && OperatingSystem.IsWindows())
            {
                return RunWithTray(app);
            }

            app.Run();
            return 0;
        }
        catch (Exception ex)
        {
            var logger = app?.Services.GetService<ILoggerFactory>()?.CreateLogger(typeof(TracksideHostRunner));
            logger?.LogCritical(ex, "Trackside host stopped during startup.");
            return 1;
        }
        finally
        {
            app?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private static int RunWithTray(WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(TracksideHostRunner));

        app.StartAsync().GetAwaiter().GetResult();
        logger.LogInformation("Trackside web host started with tray integration enabled.");

        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
        using var trayContext = ActivatorUtilities.CreateInstance<TrayApplicationContext>(app.Services);
        System.Windows.Forms.Application.Run(trayContext);

        logger.LogInformation("Trackside tray shell exited; stopping web host.");
        app.StopAsync().GetAwaiter().GetResult();
        return 0;
    }
}