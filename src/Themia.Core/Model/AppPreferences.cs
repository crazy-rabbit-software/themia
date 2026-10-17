using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>
/// [S-702] How the app behaves, as opposed to how widgets look (<see cref="DesignState"/>).
/// <para>
/// Kept apart from the design so an appearance preset or a design reset can never switch off, say,
/// game mode by accident.
/// </para>
/// </summary>
public sealed class AppPreferences
{
    public AppLanguage Language { get; set; } = AppLanguage.System;

    /// <summary>Hide the widgets while a full-screen game or presentation has the screen.</summary>
    public bool GameModeSuspend { get; set; }

    /// <summary>Whether Explorer's desktop icons are shown under the widgets.</summary>
    public bool DesktopIconsVisible { get; set; } = true;

    /// <summary>[S-3770] How every widget writes times and dates and measures temperature.</summary>
    public RegionPreferences Region { get; set; } = new();

    /// <summary>[S-3780] How often the widgets that ask an online service ask it.</summary>
    public RefreshPreferences Refresh { get; set; } = RefreshPreferences.WithDefaults();

    public AppPreferences Clone() => new()
    {
        Language = Language,
        GameModeSuspend = GameModeSuspend,
        DesktopIconsVisible = DesktopIconsVisible,
        Region = Region.Clone(),
        Refresh = Refresh.Clone(),
    };
}
