using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.Widget.WeatherFacts;

/// <summary>
/// [S-11107] The weather facts' options, all on its properties menu ([S-3719]): the facts it shows
/// (<see cref="SkyFactLines.Pick"/>: which and in what order), Appearance, the place's search
/// ([S-3722]); the temperature unit and the weather's cadence are ⚙'s, followed ([S-3590]).
/// </summary>
internal static class WeatherFactsMenu
{
    public static void Lines(WidgetMenuLines menu, WeatherFactsConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        menu.Lines.Add(SkyFactLines.Pick(menu, Glyphs.BulletList, WeatherFactsStr.WeatherFacts_Menu_Facts.Text(), config.Facts, WeatherFactRules.All, WeatherFactRules.Name, kind => WeatherFactsView.FactGlyph(kind)));
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.AddRange(WeatherPlaceRows.Lines(menu, config));
    }
}
