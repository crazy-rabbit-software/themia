using System.Text.RegularExpressions;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Modules.Tests.Modules;
using Themia.Sdk;
using Themia.Tests;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-11800] One way to size a widget: content decides the size, and nothing a widget shows appears or
/// disappears with the size of its box. The old way (a legacy sizing, the seven tiers, the disclosure
/// ladder, the size classes of the box) is gone, and these guards keep it gone.
/// <para>
/// A source guard rather than a drawn one: the view's visual tree needs WinUI and a window, which this
/// host does not have; the family render walk (<c>scripts/family-check.ps1</c>) draws every kind at its
/// min, natural and max for the look. What the guard holds is the vocabulary a view would need to pick
/// its parts by size: none of it exists, and no source (a module, the SDK, the host, a test) names it.
/// </para>
/// </summary>
public sealed partial class OnePathSizingTests
{
    /// <summary>
    /// The names of the old way. A name here is gone from the code: the legacy sizing and its floor,
    /// the tiers, the ladder and its rungs, the area's classes and breakpoints, the rules that chose by
    /// them (a row's lines by the tier, a number's ceiling by the area, a tile's glyph by the box, the
    /// detail face's plan) and the kit's ladder.
    /// </summary>
    [GeneratedRegex(@"\b(IsLegacy|LegacyFloor|WidgetSizing\.Legacy|WidgetTiers|WidgetTier|RangeTiers|Disclosure|TakeRows|WholeRows|SecondLines|SizeClass|AreaShape|WidthClass|HeightClass|WidthBreakpoints|HeightBreakpoints|KitLadder|RowRules\.Lines|RowRules\.CellIcon|ReadoutRules\.Ceiling|GlyphStep|DetailShape|DetailFace\.Plan|GaugeReading|RoomyPreviewFromWidth|FlatShedsBelow|SideShedsBelow)\b")]
    private static partial Regex OldWay();

    /// <summary>Every C# source of the repository: src and tests, build output left out, this guard too.</summary>
    private static IEnumerable<string> Sources()
    {
        static bool Kept(string file)
        {
            var parts = Path.GetRelativePath(RepoFiles.Root, file).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return !parts.Any(p => p is "bin" or "obj" or "artifacts") && !file.EndsWith(nameof(OnePathSizingTests) + ".cs", StringComparison.Ordinal);
        }

        return Directory.EnumerateFiles(RepoFiles.At("src"), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(RepoFiles.At("tests"), "*.cs", SearchOption.AllDirectories))
            .Where(Kept);
    }

    [Fact]
    public void S11800_no_source_names_the_old_way_of_sizing()
    {
        var found = new List<string>();
        foreach (var file in Sources())
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (Match match in OldWay().Matches(lines[i]))
                {
                    found.Add($"{Path.GetRelativePath(RepoFiles.Root, file).Replace('\\', '/')}:{i + 1}: {match.Value}");
                }
            }
        }

        Assert.True(found.Count == 0, "The old way of sizing is named again ([S-11800]):\n" + string.Join("\n", found));
    }

    public static TheoryData<string> Kinds() => [.. ModuleRegistry.WidgetIds];

    [Theory]
    [MemberData(nameof(Kinds))]
    public void S11800_every_kind_states_a_bounded_range_for_every_style(string id)
    {
        var kind = ModuleCatalog.Of(new WidgetId(id));
        var configs = new List<WidgetConfig> { kind.CreateDefault() };
        if (kind.CreateDefault() is IStyled styled)
        {
            foreach (var style in styled.Styles)
            {
                var config = kind.CreateDefault();
                ((IStyled)config).StyleValue = style.Value;
                configs.Add(config);
            }
        }

        foreach (var sizing in configs.Select(kind.Sizing))
        {
            Assert.True(double.IsFinite(sizing.Max.Width) && double.IsFinite(sizing.Max.Height), $"{id}: no maximum");
            Assert.True(sizing.Min.Width >= Themia.Core.Geometry.GridGeometry.MinWidgetPx && sizing.Min.Height >= Themia.Core.Geometry.GridGeometry.MinWidgetPx, $"{id}: under the floor");
        }
    }
}
