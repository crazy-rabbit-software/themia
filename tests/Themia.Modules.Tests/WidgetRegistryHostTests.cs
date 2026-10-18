using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-6907] Every widget of the build has a place of its own in the add menu, and Popular's places
/// run without a gap: over every widget of the build (Core's catalogue alone lacks the module
/// projects, whose places those are). The rest of the registry's tests are Core's WidgetRegistryTests.
/// </summary>
public sealed class WidgetRegistryHostTests
{
    [Fact]
    public void S6907_every_widget_has_a_place_of_its_own_in_the_add_menu()
    {
        var widgets = ModuleCatalog.Widgets;

        Assert.All(widgets, w => Assert.DoesNotContain(w.Add.Shelf, new[] { AddFolderId.Popular, AddFolderId.Browse, AddFolderId.More }));
        Assert.Equal(widgets.Count, widgets.Select(w => (w.Add.Shelf, w.Add.Group, w.Add.Rank)).Distinct().Count());
        var popular = widgets.Where(w => w.Add.Popular is not null).Select(w => w.Add.Popular!.Value).ToList();
        Assert.Equal(Enumerable.Range(0, popular.Count), popular.Order());
        Assert.Equal(widgets.Count, AddTree.Homes(AddTree.On([.. widgets.Select(w => w.Id)])).Count);
    }
}
