using Microsoft.Extensions.DependencyInjection;
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
        builder.Services.AddOptions<TracksideTrayOptions>()
            .Bind(builder.Configuration.GetSection(TracksideTrayOptions.SectionName))
            .ValidateOnStart();
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
}