using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Sync;

namespace Themia.Sdk;

/// <summary>
/// [S-3749] A setting a widget follows but does not own: one level of the edit bar's menus sets it
/// for every widget, and a widget's menu links to that level, never copies it ([S-3590]). A closed
/// set: each value is one level of ⚙.
/// </summary>
public enum OwnedSetting
{
    /// <summary>[S-3550] ⚙ › Accounts: the accounts an account widget reads.</summary>
    Accounts,

    /// <summary>[S-3536] ⚙ › Language &amp; region › Time format: the 12- or 24-hour clock.</summary>
    TimeFormat,

    /// <summary>[S-3536] ⚙ › Language &amp; region › Start of week.</summary>
    WeekStart,

    /// <summary>[S-3536] ⚙ › Language &amp; region › Temperature unit.</summary>
    TemperatureUnit,

    /// <summary>[S-3530] ⚙ › Online updates: how often a kind of online source is asked (<see cref="SettingLink.Cadence"/>).</summary>
    OnlineUpdates,
}

/// <summary>
/// [S-3749] Where a link row of a widget's menu leads: the setting, and for <see cref="OwnedSetting.OnlineUpdates"/>
/// the cadence row the widget's source is asked on (null for the level itself).
/// </summary>
public sealed record SettingLink(OwnedSetting Setting, CadenceRow? Cadence = null)
{
    /// <summary>[S-3749] The words of the row: what the setting is called where it is set.</summary>
    public string Text => Setting switch
    {
        OwnedSetting.Accounts => Str.WidgetsA_Office_ManageAccounts.Text(),
        OwnedSetting.TimeFormat => Str.LanguageRegion_Clock.Text(),
        OwnedSetting.WeekStart => Str.Calendar_WeekStart_Label.Text(),
        OwnedSetting.TemperatureUnit => Str.Weather_UnitLabel.Text(),
        _ => Str.EditBar_Link_UpdateInterval.Text(),
    };

    /// <summary>[S-3749] The row's icon: the setting's own, as its level shows it.</summary>
    public string Glyph => Setting switch
    {
        OwnedSetting.Accounts => Glyphs.Contact,
        OwnedSetting.TimeFormat => Glyphs.Clock,
        OwnedSetting.WeekStart => Glyphs.Calendar,
        OwnedSetting.TemperatureUnit => Glyphs.Weather,
        _ => Cadence?.Glyph ?? Glyphs.Refresh,
    };

    /// <summary>[S-3749] The owner's name, for the row's "Opens … in Settings".</summary>
    public string Owner => Setting switch
    {
        OwnedSetting.Accounts => Str.Shell_Settings_Nav_Accounts.Text(),
        OwnedSetting.OnlineUpdates => Str.SettingsMenu_OnlineUpdates.Text(),
        _ => Str.LanguageRegion_Nav.Text(),
    };

    /// <summary>
    /// [S-3749] What is in force, at the row's end beside the link's mark ("24-hour", "Monday", "°C",
    /// "Every 30 minutes"), so the widget's menu says what applies without setting it; null for Accounts.
    /// </summary>
    public string? Value => Setting switch
    {
        OwnedSetting.TimeFormat => RegionFormat.Current.Use24Hour
            ? Str.LanguageRegion_Clock24.Format(("hours", 24))
            : Str.LanguageRegion_Clock12.Format(("hours", 12)),
        OwnedSetting.WeekStart => RegionFormat.Current.WeekStart switch
        {
            WeekStartDay.Sunday => Str.Calendar_WeekStart_Sunday.Text(),
            WeekStartDay.Saturday => Str.Calendar_WeekStart_Saturday.Text(),
            _ => Str.Calendar_WeekStart_Monday.Text(),
        },
        OwnedSetting.TemperatureUnit => RegionFormat.Current.Temperature == TemperatureUnit.Fahrenheit ? "°F" : "°C",
        OwnedSetting.OnlineUpdates when Cadence is { } row => UpdateCadence.Every(UpdateCadence.Minutes(UpdateCadence.Current, row)),
        _ => null,
    };
}
