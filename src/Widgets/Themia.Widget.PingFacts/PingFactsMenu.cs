using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Themia.Widget.PingFacts;

namespace Themia.App.Shell.Config;

/// <summary>[S-2426] [S-2427] The ping details' options, all on its properties menu ([S-3719]): Appearance, the host ([S-109]), then the window. It has no settings page.</summary>
internal static class PingFactsMenu
{
    /// <summary>[S-2427] The windows offered, in probes; one not among them shows as the default.</summary>
    internal static readonly int[] Windows = [10, 30, 60, 120, 300];

    public static void Lines(WidgetMenuLines menu, PingFactsConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        menu.Lines.Add(PingHostLine.Line(menu, config.Host, value => config.Host = value));

        var current = Windows.Contains(config.EffectiveWindow) ? config.EffectiveWindow : 60;
        menu.Lines.Add(menu.Choice(Glyphs.Hours, PingFactsStr.Details_Settings_Ping_Window_Label.Text(),
            [.. Windows.Select(w => (w, PingFactsStr.Details_Settings_Ping_Window_Probes.Format(("count", LocaleFormat.Integer(w)))))],
            current, value => config.Window = value));

        // [S-11023] Which facts, in what order ([S-11007]).
        menu.Lines.Add(AtomMenuLines.Facts(menu, config));
    }
}
