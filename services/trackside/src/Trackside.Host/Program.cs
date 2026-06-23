using Trackside.Host.Hosting;

namespace Trackside.Host;

/// <summary>
/// Executable entry point for the Trackside host process.
/// </summary>
internal static class Program
{
	/// <summary>
	/// Starts the ASP.NET Core host and, when configured, the Windows tray shell.
	/// </summary>
	/// <param name="args">Command-line arguments passed through to ASP.NET Core configuration.</param>
	/// <returns>Zero when the host shuts down cleanly; non-zero when startup fails.</returns>
	[STAThread]
	private static int Main(string[] args) => TracksideHostRunner.Run(args);
}
