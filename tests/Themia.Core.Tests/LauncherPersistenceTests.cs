using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The launcher widgets' settings, through the real persistence path.
/// <para>
/// Worth pinning separately from the rest of the store's tests because these are the first configs
/// in the app to carry a <i>list</i> of their own objects. Under Native AOT the serializer is
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
    public async Task A_shortcut_list_survives_a_round_trip_in_order()
    {
        var config = new ShortcutsConfig
        {
            ClickMode = ItemClickMode.Single,
            Style = ShortcutsStyle.Details,
            GridSize = ShortcutItemSize.Large,
            ListSize = ShortcutItemSize.Small,
            GridIconOnly = true,
            GridStretchHeight = true,
            ShowFileExtension = true,
        };

        config.Items.Add(ShortcutItem.Create("https://example.com/", "Example"));
        config.Items.Add(ShortcutItem.Create(@"C:\Users\Someone\Documents"));
        config.Items.Add(ShortcutItem.Create("shell:RecycleBinFolder"));

        var reloaded = Assert.IsType<ShortcutsConfig>(await RoundTripAsync(config));

        Assert.Equal(ItemClickMode.Single, reloaded.ClickMode);
        Assert.Equal(ShortcutsStyle.Details, reloaded.Style);
        Assert.Equal(ShortcutItemSize.Large, reloaded.GridSize);
        Assert.Equal(ShortcutItemSize.Small, reloaded.ListSize);
        Assert.True(reloaded.GridIconOnly);
        Assert.True(reloaded.GridStretchHeight);
        Assert.True(reloaded.ShowFileExtension);
        Assert.Equal(3, reloaded.Items.Count);
        Assert.Equal("https://example.com/", reloaded.Items[0].Target);
        Assert.Equal("Example", reloaded.Items[0].Label);
        Assert.Equal(@"C:\Users\Someone\Documents", reloaded.Items[1].Target);
        Assert.Equal("shell:RecycleBinFolder", reloaded.Items[2].Target);

        // The id is what keeps a rename or a reorder from landing on the wrong row.
        Assert.Equal(config.Items.Select(i => i.Id), reloaded.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task A_shortcut_whose_target_would_be_refused_is_still_persisted()
    {
        // The widget refuses to open it and the settings page says why. Dropping it on load would
        // silently lose a row the user typed — and hide that something rewrote their config.
        var config = new ShortcutsConfig();
        config.Items.Add(ShortcutItem.Create("javascript:alert(1)", "Bad"));

        var reloaded = Assert.IsType<ShortcutsConfig>(await RoundTripAsync(config));

        Assert.Single(reloaded.Items);
        Assert.Equal("javascript:alert(1)", reloaded.Items[0].Target);
        Assert.False(LaunchTargets.Classify(reloaded.Items[0].Target).IsValid);
    }

    [Fact]
    public void Cloning_a_shortcut_list_copies_the_items_rather_than_sharing_them()
    {
        // Clone is what copy/paste of a widget uses. Sharing the list would make editing the copy
        // edit the original.
        var config = new ShortcutsConfig();
        config.Items.Add(ShortcutItem.Create("https://example.com/", "Example"));

        var copy = Assert.IsType<ShortcutsConfig>(config.Clone());

        copy.Items[0].Label = "Changed";
        copy.Items.Add(ShortcutItem.Create(@"C:\Temp"));

        Assert.Single(config.Items);
        Assert.Equal("Example", config.Items[0].Label);

        // A fresh id, so two copies of a shortcut are two rows rather than one row twice.
        Assert.NotEqual(config.Items[0].Id, copy.Items[0].Id);
    }
}
