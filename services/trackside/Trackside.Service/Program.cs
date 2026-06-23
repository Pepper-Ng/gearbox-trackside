using Trackside.Service.Hosting;

namespace Trackside.Service;

/// <summary>
/// Executable entry point for the Trackside service process.
/// </summary>
internal static class Program
{
	/// <summary>
	/// Starts the ASP.NET Core service host.
	/// </summary>
	/// <param name="args">Command-line arguments passed through to ASP.NET Core configuration.</param>
	/// <returns>Zero when the host shuts down cleanly; non-zero when startup fails.</returns>
	[STAThread]
	private static int Main(string[] args) => TracksideServiceRunner.Run(args);
}
