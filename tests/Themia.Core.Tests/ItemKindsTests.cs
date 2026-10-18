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
}
