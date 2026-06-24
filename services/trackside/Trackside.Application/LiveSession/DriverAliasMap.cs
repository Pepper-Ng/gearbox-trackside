namespace Trackside.Application.LiveSession;

/// <summary>
/// Resolves staff-entered display aliases for fixed rig names.
/// </summary>
public sealed class DriverAliasMap
{
    private readonly IReadOnlyDictionary<string, string> _aliases;

    /// <summary>
    /// Empty alias map used when no staff/config aliases are available.
    /// </summary>
    public static DriverAliasMap Empty { get; } = new(null);

    /// <summary>
    /// Creates an alias map from rig-name keys to display-name values.
    /// </summary>
    /// <param name="aliases">Configured aliases.</param>
    public DriverAliasMap(IReadOnlyDictionary<string, string>? aliases)
    {
        _aliases = aliases is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(aliases, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns the configured display name for a rig, or the rig name when no alias exists.
    /// </summary>
    /// <param name="rigName">Underlying rFactor 2 driver or rig name.</param>
    /// <returns>Display name for the current leaderboard row.</returns>
    public string Resolve(string rigName)
    {
        if (string.IsNullOrWhiteSpace(rigName))
        {
            return string.Empty;
        }

        return _aliases.TryGetValue(rigName, out var alias) && !string.IsNullOrWhiteSpace(alias)
            ? alias.Trim()
            : rigName;
    }
}