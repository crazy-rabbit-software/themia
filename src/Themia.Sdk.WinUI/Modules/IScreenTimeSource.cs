using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>
/// [S-458] Where a screen-time widget and its settings page read from (<see cref="Themia.Sdk.WinUI.IWidgetHost.ScreenTime"/>,
/// <see cref="Themia.Sdk.WinUI.SettingsHost.ScreenTime"/>): the app's one tracker. The foreground hook,
/// the tracker and its file (<c>usage_stats.json</c>) stay the host's.
/// </summary>
public interface IScreenTimeSource
{
    /// <summary>The source's own idea of today, so a widget and the tracker never disagree about midnight.</summary>
    LocalDay Today { get; }

    /// <summary>[S-454] One day, live-inclusive. Null means today.</summary>
    ScreenTimeDay Day(string? date);

    /// <summary>[S-454] The rolling window ending today, oldest first, gap days as zeros.</summary>
    IReadOnlyList<ScreenTimeDay> Range(int days);

    /// <summary>
    /// [S-3322] The hidden apps and limits every screen-time widget shares. Read-only: change them
    /// through <see cref="SetPreferences"/>, which replaces the whole set.
    /// </summary>
    ScreenTimePreferences Preferences { get; }

    /// <summary>[S-3322] Replace the shared preferences, save them and tell every widget. UI thread.</summary>
    void SetPreferences(ScreenTimePreferences preferences);

    /// <summary>Raised on the UI thread when there is something new to draw.</summary>
    event Action? Changed;
}
