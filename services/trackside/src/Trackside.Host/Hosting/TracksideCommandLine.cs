namespace Trackside.Host.Hosting;

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
    public static string[] Normalize(string[] args)
    {
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
                case "--no-tray":
                    normalized.Add(ToConfigurationValue("Trackside:Tray:Enabled", "false"));
                    break;
                case "--tray":
                    normalized.Add(ToConfigurationValue("Trackside:Tray:Enabled", "true"));
                    break;
                default:
                    normalized.Add(argument);
                    break;
            }
        }

        return [.. normalized];
    }

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