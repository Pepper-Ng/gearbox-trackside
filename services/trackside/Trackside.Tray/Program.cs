using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Windows.Forms;
using Trackside.Tray.Tray;

namespace Trackside.Tray;

/// <summary>
/// Entry point for the interactive Trackside tray companion.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Starts the tray companion in the current interactive Windows session.
    /// </summary>
    /// <param name="args">Command-line arguments passed to the generic host.</param>
    [STAThread]
    private static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        AddExternalConfiguration(builder, args);
        builder.Services.AddOptions<TracksideTrayOptions>()
            .Bind(builder.Configuration.GetSection(TracksideTrayOptions.SectionName))
            .ValidateOnStart();
        builder.Services.AddSingleton<HttpClient>();
        builder.Services.AddSingleton<ITrayStatusClient, TrayStatusClient>();
        builder.Services.AddSingleton<TrayApplicationContext>();

        using var host = builder.Build();
        host.Start();

        var options = host.Services.GetRequiredService<IOptions<TracksideTrayOptions>>().Value;
        if (!options.Enabled)
        {
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using var trayContext = host.Services.GetRequiredService<TrayApplicationContext>();
        Application.Run(trayContext);
    }

    private static void AddExternalConfiguration(HostApplicationBuilder builder, string[] args)
    {
        var configRoot = ReadOption(args, "--config-root") ?? Environment.GetEnvironmentVariable("TRACKSIDE_CONFIG_ROOT");
        if (string.IsNullOrWhiteSpace(configRoot))
        {
            return;
        }

        var trayConfigRoot = Path.Combine(configRoot, "tray");
        builder.Configuration
            .AddJsonFile(Path.Combine(trayConfigRoot, "appsettings.json"), optional: true, reloadOnChange: true)
            .AddJsonFile(Path.Combine(trayConfigRoot, $"appsettings.{builder.Environment.EnvironmentName}.json"), optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args);
    }

    private static string? ReadOption(string[] args, string optionName)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], optionName, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }
}