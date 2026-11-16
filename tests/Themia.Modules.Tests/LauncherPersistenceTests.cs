using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Widget.DesktopItem;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// The launcher widgets' settings, through the real persistence path.
/// <para>
/// Worth pinning separately from the rest of the store's tests because a shortcut's config carries
/// a <i>tree</i> of its own objects (an app layout). Under Native AOT the serializer is
/// source generated, so a nested type the generator did not pick up fails at runtime in the
/// published build and nowhere else — a round trip is the cheapest way to say it did.
/// </para>
/// </summary>
public sealed class LauncherPersistenceTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public LauncherPersistenceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "themia-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "state.json");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // ignore: temp cleanup is best effort and must not fail a test run
        }
    }

    private AppStateStore NewStore() => new(_path, TimeSpan.Zero);

    private async Task<WidgetConfig> RoundTripAsync(WidgetConfig config)
    {
        var state = AppState.CreateDefault();
        var widget = state.ActiveView().Widgets[0];

        widget.Config = config;

        var store = NewStore();
        store.SaveSoon(state);
        await store.FlushAsync();

        var loaded = NewStore().Load();

        Assert.Equal(LoadOutcome.Loaded, loaded.Outcome);
        return loaded.State.ActiveView().Widgets[0].Config;
    }

    [Fact]
    public async Task A_shortcut_survives_a_round_trip_with_its_id()
    {
        var config = new DesktopItemConfig
        {
            Target = "https://example.com/",
            Label = "Example",
            IconEmoji = "★",
            LabelLines = 1,
            ClickMode = ItemClickMode.Single,
        };

        var reloaded = Assert.IsType<DesktopItemConfig>(await RoundTripAsync(config));

        Assert.Equal(("https://example.com/", "Example", "★", 1, ItemClickMode.Single),
            (reloaded.Target, reloaded.Label, reloaded.IconEmoji, reloaded.LabelLines, reloaded.ClickMode));

        // The id is what the layout editor finds a layout by: it is the stored one, never a new one per load.
        Assert.Equal(config.ItemId, reloaded.ItemId);
    }

    [Fact]
    public async Task An_app_layout_survives_a_round_trip_as_a_tree()
    {
        // The config carries a tree of its own objects: under Native AOT a nested type the generator
        // did not pick up fails at run time in the published build and nowhere else.
        var config = new DesktopItemConfig { Label = "Morning" };
        config.MakeLayout();
        (config.Layout, _) = LayoutTree.Split(config.Layout!, config.Layout!.Id, SplitDirection.Vertical);
        LayoutTree.Assign(config.Layout!, config.Layout!.A!.Id, new LayoutAssignment { Source = LayoutSource.App, Target = @"C:\Windows\notepad.exe" });

        var reloaded = Assert.IsType<DesktopItemConfig>(await RoundTripAsync(config));

        Assert.True(reloaded.IsLayout);
        Assert.Equal(LayoutNodeKind.Split, reloaded.Layout!.Kind);
        Assert.Equal(@"C:\Windows\notepad.exe", reloaded.Layout.A!.Assignment!.Target);
        Assert.Equal(config.ItemId, reloaded.ItemId);
    }

    [Fact]
    public async Task A_shortcut_whose_target_would_be_refused_is_still_persisted()
    {
        // The widget refuses to open it and its tooltip says why. Dropping it on load would
        // silently lose what the user typed — and hide that something rewrote their config.
        var config = new DesktopItemConfig { Target = "javascript:alert(1)", Label = "Bad" };

        var reloaded = Assert.IsType<DesktopItemConfig>(await RoundTripAsync(config));

        Assert.Equal("javascript:alert(1)", reloaded.Target);
        Assert.False(LaunchTargets.Classify(reloaded.Target).IsValid);
    }

    [Fact]
    public void Cloning_a_shortcut_copies_its_layout_rather_than_sharing_it()
    {
        // Clone is what copy/paste of a widget uses. Sharing the tree would make editing the copy
        // edit the original.
        var config = new DesktopItemConfig { Label = "Example" };
        config.MakeLayout();

        var copy = Assert.IsType<DesktopItemConfig>(config.Clone());

        copy.Label = "Changed";
        copy.Layout!.Assignment = new LayoutAssignment { Source = LayoutSource.Url, Target = "https://example.com/" };

        Assert.Equal("Example", config.Label);
        Assert.Null(config.Layout!.Assignment);

        // A fresh id, so the layout editor never edits two shortcuts at once.
        Assert.NotEqual(config.ItemId, copy.ItemId);
    }
}
