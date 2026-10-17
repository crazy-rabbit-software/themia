using System.Runtime.CompilerServices;
using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Storage;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-44] A state file of the shape users have: one widget of every kind with its default config,
/// generated once (Fixtures/state-every-kind.json, ids w00.., not anyone's real file). The module
/// registry work (docs/WIDGET-KIT.md section 11.1, M2) moves every per-kind table; this pins that
/// such a file still loads with no repair and writes back byte for byte.
/// </summary>
public sealed class StateFixtureTests
{
    private static string Fixture([CallerFilePath] string here = "")
        => Path.Combine(Path.GetDirectoryName(here)!, "Fixtures", "state-every-kind.json");

    private static string Normalised(string json) => json.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');

    [Fact]
    public void S44_a_state_with_every_kinds_default_config_loads_without_a_repair()
    {
        var parsed = AppStateStore.Parse(File.ReadAllText(Fixture()));

        Assert.NotNull(parsed.State);
        Assert.Empty(parsed.Repairs);
        var widgets = Assert.Single(parsed.State.Views).Widgets;
        Assert.Equal(Enum.GetValues<WidgetKind>().Length, widgets.Count);
        Assert.Equal(Enum.GetValues<WidgetKind>().Order(), widgets.Select(w => w.Kind).Order());
    }

    [Fact]
    public void S44_a_state_with_every_kinds_default_config_writes_back_unchanged()
    {
        var json = File.ReadAllText(Fixture());
        var parsed = AppStateStore.Parse(json);

        Assert.NotNull(parsed.State);
        Assert.Equal(Normalised(json), Normalised(JsonSerializer.Serialize(parsed.State, AppStateJsonContext.Default.AppState)));
    }
}
