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
    public RefreshPreferences Refresh { get; set; } = new();

    public AppPreferences Clone() => new()
    {
        Language = Language,
        GameModeSuspend = GameModeSuspend,
        DesktopIconsVisible = DesktopIconsVisible,
        Region = Region.Clone(),
        Refresh = Refresh.Clone(),
    };
}

/// <summary>
/// [S-3780] The four update cadences, in minutes, one per kind of online source. Each is snapped to
/// its offered choices on load (<see cref="Sync.UpdateCadence"/>), so a hand edit cannot ask a free
/// service every second.
/// </summary>
public sealed class RefreshPreferences
{
    /// <summary>Weather now and forecast (open-meteo publishes every 15 minutes).</summary>
    public int WeatherMinutes { get; set; } = 30;

    /// <summary>Stocks (Yahoo, CoinGecko).</summary>
    public int MarketsMinutes { get; set; } = 5;

    /// <summary>News feeds.</summary>
    public int FeedsMinutes { get; set; } = 30;

    /// <summary>Mail, calendars and task lists from the signed-in accounts.</summary>
    public int AccountsMinutes { get; set; } = 2;

    public RefreshPreferences Clone() => new()
    {
        WeatherMinutes = WeatherMinutes,
        MarketsMinutes = MarketsMinutes,
        FeedsMinutes = FeedsMinutes,
        AccountsMinutes = AccountsMinutes,
    };
}
