using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Sync;

namespace Themia.Core.Settings;

/// <summary>
/// [S-3530] Online updates ›: how often each kind of online source is asked ([S-3780]), for every
/// widget instead of one setting each: one line per cadence, the host's then any provider's own
/// ([S-6910]), its submenu the intervals with the dot on the one that holds.
/// </summary>
public static partial class SettingsMenu
{
    /// <summary>[S-3530] The Online updates line and its submenu.</summary>
    public static SettingsSubmenu OnlineUpdates(ISettingsMenuHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        var refresh = host.State.Preferences.Refresh;
        var lines = new List<SettingsLine>();
        foreach (var row in UpdateCadence.Rows)
        {
            var key = Keys.Online + "/" + row.Id.Value;
            var current = UpdateCadence.Minutes(refresh, row);
            var options = new List<SettingsRadio>(row.Choices.Count);
            foreach (var minutes in row.Choices)
            {
                var holds = minutes == current;
                options.Add(new SettingsRadio(key + "/" + minutes, UpdateCadence.Every(minutes), holds, () =>
                {
                    if (!holds)
                    {
                        host.Mutate(state => UpdateCadence.Set(state.Preferences.Refresh, row, minutes), relayout: false);
                    }
                }));
            }

            lines.Add(new SettingsChoice(key, row.Label.Text(), row.Glyph, options));
        }

        lines.Add(new SettingsSeparator(Keys.Online + "/-note"));
        lines.Add(new SettingsNote(Keys.Online + "/note", Str.WidgetPrefs_Updates_Note.Text()));

        return new SettingsSubmenu(Keys.Online, Str.SettingsMenu_OnlineUpdates.Text(), Glyphs.Refresh, lines);
    }
}
