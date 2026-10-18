using System.Text.RegularExpressions;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Tests;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-6570]..[S-6572] Which kinds have items an item background reaches, and the panel's rule for the
/// ones that do not. The declaration (<see cref="ItemKinds"/>) is checked against the views' source,
/// so it cannot drift from what the views paint.
/// </summary>
public sealed partial class ItemKindsTests
{
    /// <summary>What paints an item directly: the one path ([S-6513]) and Fluent's row helpers, which call it.</summary>
    [GeneratedRegex(@"\bItemFill\.|\bFluent\.(Row|Hover)\(")]
    private static partial Regex PaintsDirectly();

    /// <summary>A top-level type a file declares (file-scoped namespaces: at the line's start).</summary>
    [GeneratedRegex(@"^(?:internal |public )?(?:sealed |static |abstract )*(?:partial )?(?:class|struct|record(?: struct)?) (\w+)", RegexOptions.Multiline)]
    private static partial Regex TopLevelType();

    /// <summary>
    /// Every view file that paints an item: directly, or through a shared part (a type that is not a
    /// view, declared in a file that paints one: the flip card, a ring, a meter's bars) — to the
    /// fixed point, so a part built from another part counts too. Comments are not code.
    /// </summary>
    private static HashSet<string> PaintingFiles()
    {
        var skip = new HashSet<string>(StringComparer.Ordinal) { "ItemPalette.cs", "ItemFill.cs", "Fluent.cs", "WidgetCatalog.cs" };

        // The shared parts a view may paint through (a ring) live in the SDK since they serve every module.
        var files = RepoFiles.ViewSources()
            .Where(f => !skip.Contains(Path.GetFileName(f)))
            .ToDictionary(f => f, f => string.Join('\n', File.ReadAllLines(f).Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal))));

        var partTypes = new HashSet<string>(StringComparer.Ordinal);
        var painting = new HashSet<string>(StringComparer.Ordinal);
        for (var grew = true; grew;)
        {
            grew = false;
            foreach (var (path, text) in files)
            {
                if (painting.Contains(path)
                    || !(PaintsDirectly().IsMatch(text) || partTypes.Any(p => Regex.IsMatch(text, $@"\b{p}\b"))))
                {
                    continue;
                }

                painting.Add(path);
                grew = true;
                foreach (Match m in TopLevelType().Matches(text))
                {
                    if (!m.Groups[1].Value.EndsWith("View", StringComparison.Ordinal))
                    {
                        partTypes.Add(m.Groups[1].Value);
                    }
                }
            }
        }

        return painting;
    }

    [Fact]
    public void S6570_the_declaration_matches_what_every_view_paints_both_ways()
    {
        var painting = PaintingFiles().Select(Path.GetFileName).ToList();
        var views = ModuleSources.Views();
        var wrong = new List<string>();

        foreach (var kind in ModuleCatalog.Widgets.Select(w => w.Id).ToArray())
        {
            Assert.True(views.TryGetValue(kind, out var view), $"{kind} has no view in its module's CreateView");
            var paints = painting.Any(f => f!.StartsWith(view + ".", StringComparison.Ordinal));
            if (paints != ItemKinds.Has(kind))
            {
                wrong.Add($"{kind} ({view}) paints items: {paints}, declared: {ItemKinds.Has(kind)}");
            }
        }

        Assert.True(wrong.Count == 0, "ItemKinds disagrees with the views:\n" + string.Join('\n', wrong));
    }

    [Fact]
    public void S6570_lists_and_meters_have_items_and_a_plain_readout_does_not()
    {
        Assert.True(ItemKinds.Has(TasksConfig.Descriptor.Id));
        Assert.True(ItemKinds.Has(CpuConfig.Descriptor.Id));
        Assert.True(ItemKinds.Has(ClipboardConfig.Descriptor.Id));
        Assert.False(ItemKinds.Has(AlarmConfig.Descriptor.Id));
        Assert.False(ItemKinds.Has(GalleryConfig.Descriptor.Id));
    }

    private static (AppState State, ViewState View, WidgetSelection Selection) Selected(params WidgetId[] kinds)
    {
        var view = ViewState.Create("v");
        for (var i = 0; i < kinds.Length; i++)
        {
            var widget = WidgetInstance.Create(kinds[i], new FracRect(0, 0, 0.1, 0.1));
            widget.Id = $"w{i}";
            view.Widgets.Add(widget);
        }

        var selection = new WidgetSelection();
        selection.Replace(view.Id, [.. view.Widgets.Select(w => w.Id)]);
        return (new AppState { Views = [view] }, view, selection);
    }

    [Fact]
    public void S6571_a_widget_without_items_offers_no_item_background()
    {
        var (state, view, selection) = Selected(AlarmConfig.Descriptor.Id);

        Assert.False(QuickSet.For(state, selection, view.Id).HasItems);
        Assert.Equal(0, QuickSet.SetItemBackground(view, ["w0"], FillChoice.Transparent()));
        Assert.Equal(FillKind.Default, view.Widgets[0].ItemBackground.Kind);

        // The item text is the items' too: none to colour.
        Assert.Equal(0, QuickSet.SetItemTextColor(view, ["w0"], "#FFFFFF"));
        Assert.Equal(string.Empty, view.Widgets[0].ItemTextColor);
    }

    [Fact]
    public void S6572_a_mixed_selection_offers_it_when_any_has_items_and_sets_only_those()
    {
        var (state, view, selection) = Selected(AlarmConfig.Descriptor.Id, TasksConfig.Descriptor.Id);
        view.Widgets[0].ItemBackground = FillChoice.Of("#123456", 1);

        var offer = QuickSet.For(state, selection, view.Id);
        Assert.True(offer.HasItems);

        // The value read is the one the widgets with items share; the one without has none to show.
        Assert.Equal(FillKind.Default, offer.Items?.Kind);

        Assert.Equal(1, QuickSet.SetItemBackground(view, ["w0", "w1"], FillChoice.Transparent()));
        Assert.Equal(FillKind.Colour, view.Widgets[0].ItemBackground.Kind);
        Assert.Equal(FillKind.Transparent, view.Widgets[1].ItemBackground.Kind);
    }
}
