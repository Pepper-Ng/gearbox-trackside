namespace Trackside.Service.Hosting;

/// <summary>
/// Converts friendly Trackside command-line switches into ASP.NET Core configuration keys.
/// </summary>
public static class TracksideCommandLine
{
    /// <summary>
    /// Converts user-facing arguments into configuration-provider arguments consumed by ASP.NET Core.
    /// </summary>
    /// <param name="args">Raw process arguments.</param>
    /// <returns>Arguments that can be passed to <see cref="WebApplicationOptions.Args" />.</returns>
    public static string[] Normalize(string[] args) => Normalize(args, out _);

    /// <summary>
    /// Converts user-facing arguments and reports whether the process should force console lifetime.
    /// </summary>
    /// <param name="args">Raw process arguments.</param>
    /// <param name="forceConsoleMode">True when <c>--console</c> was supplied.</param>
    /// <param name="configRoot">External configuration root passed through <c>--config-root</c>, when supplied.</param>
    /// <returns>Arguments that can be passed to <see cref="WebApplicationOptions.Args" />.</returns>
    public static string[] Normalize(string[] args, out bool forceConsoleMode, out string? configRoot)
    {
        forceConsoleMode = false;
        configRoot = Environment.GetEnvironmentVariable("TRACKSIDE_CONFIG_ROOT");
        var normalized = new List<string>();
        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--source":
                    normalized.Add(ToConfigurationValue("Trackside:Source:Mode", ReadValue(args, ref index, argument)));
                    break;
                case "--fixture":
                    normalized.Add(ToConfigurationValue("Trackside:Source:FixturePath", ReadValue(args, ref index, argument)));
                    break;
                case "--listen-url":
                    normalized.Add(ToConfigurationValue("Trackside:Http:ListenUrl", ReadValue(args, ref index, argument)));
                    break;
                case "--public-base-url":
                    normalized.Add(ToConfigurationValue("Trackside:Http:PublicBaseUrl", ReadValue(args, ref index, argument)));
                    break;
                case "--config-root":
                    configRoot = ReadValue(args, ref index, argument);
                    normalized.Add(ToConfigurationValue("Trackside:Deployment:ConfigPath", configRoot));
                    break;
                case "--install-mode":
                    normalized.Add(ToConfigurationValue("Trackside:Deployment:InstallMode", ReadValue(args, ref index, argument)));
                    break;
                case "--install-root":
                    normalized.Add(ToConfigurationValue("Trackside:Deployment:InstallRoot", ReadValue(args, ref index, argument)));
                    break;
                case "--data-path":
                    normalized.Add(ToConfigurationValue("Trackside:Deployment:DataPath", ReadValue(args, ref index, argument)));
                    break;
                case "--logs-path":
                    normalized.Add(ToConfigurationValue("Trackside:Deployment:LogsPath", ReadValue(args, ref index, argument)));
                    break;
                case "--updates-path":
                    normalized.Add(ToConfigurationValue("Trackside:Deployment:UpdatesPath", ReadValue(args, ref index, argument)));
                    break;
                case "--bundle-version":
                    normalized.Add(ToConfigurationValue("Trackside:Deployment:BundleVersion", ReadValue(args, ref index, argument)));
                    break;
                case "--manifest-path":
                    normalized.Add(ToConfigurationValue("Trackside:Deployment:ManifestPath", ReadValue(args, ref index, argument)));
                    break;
                case "--service-name":
                    normalized.Add(ToConfigurationValue("Trackside:Deployment:ServiceName", ReadValue(args, ref index, argument)));
                    break;
                case "--update-manifest-url":
                    normalized.Add(ToConfigurationValue("Trackside:Updates:ManifestUrl", ReadValue(args, ref index, argument)));
                    break;
                case "--update-candidate-manifest":
                    normalized.Add(ToConfigurationValue("Trackside:Updates:CandidateManifestPath", ReadValue(args, ref index, argument)));
                    break;
                case "--console":
                    forceConsoleMode = true;
                    break;
                default:
                    normalized.Add(argument);
                    break;
            }
        }

        return [.. normalized];
    }

    /// <summary>
    /// Converts user-facing arguments and reports whether the process should force console lifetime.
    /// </summary>
    /// <param name="args">Raw process arguments.</param>
    /// <param name="forceConsoleMode">True when <c>--console</c> was supplied.</param>
    /// <returns>Arguments that can be passed to <see cref="WebApplicationOptions.Args" />.</returns>
    public static string[] Normalize(string[] args, out bool forceConsoleMode) => Normalize(args, out forceConsoleMode, out _);

    private static string ReadValue(string[] args, ref int index, string argumentName)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Command-line argument {argumentName} requires a value.", argumentName);
        }

        index++;
        return args[index];
    }

    private static string ToConfigurationValue(string key, string value) => $"--{key}={value}";
}