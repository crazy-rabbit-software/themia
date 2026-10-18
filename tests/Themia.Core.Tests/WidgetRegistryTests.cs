using System.Text.Json;
using System.Text.RegularExpressions;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Tests;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-6902] The host without its widgets: every widget is a module project, so Core and the app
/// declare none, and Core's tests run over a registry of none (the module host's
/// WidgetRegistryHostTests check the registry of every widget of the build).
/// </summary>
public sealed partial class WidgetRegistryTests
{
    [Fact]
    public void S6902_neither_core_nor_the_app_declares_a_widget()
    {
        Assert.DoesNotContain(typeof(AppState).Assembly.GetTypes(), t => t.IsSubclassOf(typeof(WidgetConfig)) && !t.IsAbstract);
        Assert.Empty(ModuleCatalog.Widgets);

        var app = Path.Combine(RepoFiles.Root, "src", "Themia.App");
        var declared = Directory.EnumerateFiles(app, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => ThemiaWidget().IsMatch(File.ReadAllText(f)))
            .ToList();
        Assert.Empty(declared);
        Assert.Empty(Directory.GetFiles(Path.Combine(app, "Shell", "Config"), "WidgetSettings*.cs"));
    }

    /// <summary>[S-6903] Regression: with no widget installed the state options refused to build ("should specify at least one derived type").</summary>
    [Fact]
    public void S6903_a_host_with_no_widget_reads_and_writes_a_state_without_widgets()
    {
        var json = JsonSerializer.Serialize(new AppState(), StateJson.AppState);
        var back = JsonSerializer.Deserialize(json, StateJson.AppState);

        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back!, StateJson.AppState));
    }

    [GeneratedRegex(@"\[assembly:\s*ThemiaWidget\(")]
    private static partial Regex ThemiaWidget();
}
