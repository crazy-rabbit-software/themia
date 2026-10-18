using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Widget.PingDetails;

namespace Themia.App.Shell.Config;

/// <summary>[S-2426] [S-2427] The ping details' settings page: the host (the shared host row), then the window.</summary>
internal static class PingDetailsSettings
{
    /// <summary>[S-2426] [S-2427] The host, as the ping meter's page has it, then the window.</summary>
    public static void Rows(List<FrameworkElement> rows, PingDetailsConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(PingDetailsStr.Details_Catalog_Ping_Label.Text()));
        PingHostRow.Add(rows, () => config.Host, value => config.Host = value, save);

        int[] windows = [10, 30, 60, 120, 300];
        var current = windows.Contains(config.EffectiveWindow) ? config.EffectiveWindow : 60;

        rows.Add(FluentLayout.Row(Glyphs.Hours, PingDetailsStr.Details_Settings_Ping_Window_Label.Text(), PingDetailsStr.Details_Settings_Ping_Window_Desc.Text(),
            FluentLayout.Choice(windows.Select(w => (w, PingDetailsStr.Details_Settings_Ping_Window_Probes.Format(("count", LocaleFormat.Integer(w))))).ToList(), current,
                value => { config.Window = value; save(); }),
            "window", "samples"));
    }
}
