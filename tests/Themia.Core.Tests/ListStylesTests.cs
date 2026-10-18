using Themia.Core.Habits;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-6400]..[S-6407] The styles of the list and media widgets: Tasks, Shortcuts, Gallery, Mail and
/// Now playing (the quote's and the habit tracker's are their modules'). Each was a look option before ([S-6000]); these pin what each
/// style is, and the rules a new look brought (the large mark, the heatmap, the checklist).
/// </summary>
public class ListStylesTests
{
    [Fact]
    public void S6401_only_the_grid_draws_with_the_tile_settings()
    {
        Assert.Contains(ShortcutsSetting.TileSize, new ShortcutsConfig { Style = ShortcutsStyle.Grid }.ShownSettings);
        Assert.DoesNotContain(ShortcutsSetting.TileSize, new ShortcutsConfig { Style = ShortcutsStyle.List }.ShownSettings);
        Assert.DoesNotContain(ShortcutsSetting.TileSize, new ShortcutsConfig { Style = ShortcutsStyle.Details }.ShownSettings);
    }

}
