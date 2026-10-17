using System.Runtime.CompilerServices;
using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Sdk;
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

    /// <summary>
    /// The fixture as this build writes it: files from before module ids also gave each widget its
    /// kind beside its config's ([S-6908]); that copy is not read and no longer written.
    /// </summary>
    private static System.Text.Json.Nodes.JsonNode WithoutWidgetKinds(string json)
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        foreach (var widget in root["views"]![0]!["widgets"]!.AsArray())
        {
            widget!.AsObject().Remove("kind");
        }

        return root;
    }

    [Fact]
    public void S44_a_state_with_every_kinds_default_config_loads_without_a_repair()
    {
        var parsed = AppStateStore.Parse(File.ReadAllText(Fixture()));

        Assert.NotNull(parsed.State);
        Assert.Empty(parsed.Repairs);
        var widgets = Assert.Single(parsed.State.Views).Widgets;
        Assert.Equal(ModuleCatalog.Widgets.Select(w => w.Id).ToArray().Length, widgets.Count);
        Assert.Equal(ModuleCatalog.Widgets.Select(w => w.Id).ToArray().Order(), widgets.Select(w => w.Kind).Order());
    }

    [Fact]
    public void S44_a_state_with_every_kinds_default_config_writes_back_unchanged()
    {
        var json = File.ReadAllText(Fixture());
        var parsed = AppStateStore.Parse(json);

        Assert.NotNull(parsed.State);
        var written = JsonSerializer.Serialize(parsed.State, StateJson.AppState);
        Assert.True(
            System.Text.Json.Nodes.JsonNode.DeepEquals(WithoutWidgetKinds(json), System.Text.Json.Nodes.JsonNode.Parse(written)),
            "the every-kind state does not write back as it was read");
    }
}
