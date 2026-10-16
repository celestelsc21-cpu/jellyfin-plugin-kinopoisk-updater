using System;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.KinopoiskUpdater.Configuration;

/// <summary>
/// Plugin settings persisted by the server as XML.
/// </summary>
/// <remarks>
/// New properties must have safe defaults: configurations saved by older plugin
/// versions are deserialized without them.
/// </remarks>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Default number of actors written to an item.
    /// </summary>
    public const int DefaultMaxActors = 30;

    /// <summary>
    /// Upper bound for <see cref="MaxActors"/>.
    /// </summary>
    public const int MaxActorsLimit = 200;

    /// <summary>
    /// Maximum accepted API key length; real keys are much shorter.
    /// </summary>
    public const int ApiKeyMaxLength = 128;

    /// <summary>
    /// Gets or sets the kinopoiskapiunofficial.tech API key (sent as the X-API-KEY header).
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum number of actors written to an item (crew is not limited).
    /// </summary>
    public int MaxActors { get; set; } = DefaultMaxActors;

    /// <summary>
    /// Gets a value indicating whether an API key is configured.
    /// </summary>
    public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>
    /// Brings values received from the settings page or a hand-edited file into valid ranges.
    /// </summary>
    public void Normalize()
    {
        var key = (ApiKey ?? string.Empty).Trim();
        ApiKey = key.Length <= ApiKeyMaxLength && IsPrintableAscii(key) ? key : string.Empty;
        MaxActors = Math.Clamp(MaxActors, 0, MaxActorsLimit);
    }

    // A header value must not contain control characters (header injection) or non-ASCII.
    private static bool IsPrintableAscii(string value)
    {
        foreach (var c in value)
        {
            if (c < 0x21 || c > 0x7E)
            {
                return false;
            }
        }

        return true;
    }
}
