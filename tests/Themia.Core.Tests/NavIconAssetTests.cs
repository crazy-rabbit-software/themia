using System.Text.RegularExpressions;
using System.Xml.Linq;
using Themia.Tests;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The settings navigation's bundled colour icons ([S-1600..S-1604]). They are files, not code, so
/// these tests read the repository: every icon the app names exists, every bundled icon carries its
/// licence notice, and none uses an SVG feature the Direct2D renderer behind SvgImageSource skips
/// (an icon with a filter or mask would draw without it, silently, in only some places).
/// </summary>
public sealed partial class NavIconAssetTests
{
    private static readonly string Root = RepoFiles.Root;

    private static string IconDir => Path.Combine(Root, "src", "Themia.App", "Assets", "NavIcons");

    /// <summary>
    /// Walks up from this source file rather than the test binaries, which may be built into an
    /// artifacts folder outside the repository.
    /// </summary>

    private static string[] BundledIcons()
        => Directory.GetFiles(IconDir, "*.svg").Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal).ToArray();

    [GeneratedRegex(@"""(ic_fluent_[a-z0-9_]+_color\.svg)""")]
    private static partial Regex IconName();

    [Fact]
    public void S1600_every_icon_the_navigation_names_is_bundled_and_every_bundled_icon_is_named()
    {
        var source = File.ReadAllText(Path.Combine(Root, "src", "Themia.App", "Shell", "Config", "NavIcons.cs"));
        var named = IconName().Matches(source).Select(m => m.Groups[1].Value).Distinct().Order(StringComparer.Ordinal).ToArray();

        Assert.NotEmpty(named);
        Assert.Equal(named, BundledIcons());
    }

    [Fact]
    public void S1603_every_bundled_icon_is_listed_under_its_licence()
    {
        var notices = File.ReadAllText(Path.Combine(Root, "THIRD-PARTY-NOTICES.md"));
        var section = notices[notices.IndexOf("## Fluent UI System Icons", StringComparison.Ordinal)..];

        Assert.Contains("MIT License", section, StringComparison.Ordinal);
        Assert.Contains("Copyright (c) 2020 Microsoft Corporation", section, StringComparison.Ordinal);
        Assert.All(BundledIcons(), file => Assert.Contains($"`{file}`", section, StringComparison.Ordinal));
    }

    [Fact]
    public void S1604_icons_use_only_what_the_svg_renderer_draws()
    {
        // Shapes, groups and gradient fills. Direct2D's SVG support has no filters, and Fluent's
        // colour icons that use masks or blurs would lose their shading without a word.
        string[] allowed = ["svg", "defs", "g", "path", "rect", "circle", "ellipse", "linearGradient", "radialGradient", "stop"];

        Assert.NotEmpty(BundledIcons());
        foreach (var file in BundledIcons())
        {
            var svg = XDocument.Load(Path.Combine(IconDir, file));
            var unknown = svg.Descendants().Select(e => e.Name.LocalName).Where(n => !allowed.Contains(n)).Distinct().ToArray();

            Assert.True(unknown.Length == 0, $"{file} uses {string.Join(", ", unknown)}");
            Assert.Equal("0 0 20 20", svg.Root?.Attribute("viewBox")?.Value);
        }
    }
}
