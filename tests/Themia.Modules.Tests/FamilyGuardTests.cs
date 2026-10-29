using System.Text.RegularExpressions;
using Themia.Sdk.Kit;
using Themia.Tests;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-6730] The family's source guard: no size, space, radius, stroke, weight or colour literal outside
/// the family's tokens in any place a widget is drawn from — every module (<c>src/Widgets/**</c>), the
/// SDK's UI parts (<c>src/Themia.Sdk.WinUI/**</c>) and the app's cards and views
/// (<c>src/Themia.App/Shell/**</c>, <c>src/Themia.App/Widgets/**</c>); a module's <c>Tests/</c> and
/// <c>Fixtures/</c> and the debug renders (<c>Gallery/</c>) left out.
/// <para>
/// It started (2026-10-29) with a list of the day's offenders that every phase of the family had to
/// shrink; the shell sweep emptied it and the list is gone: any offence fails the test now. A value the
/// family has no token for is a token first (an SDK change with its test), and a measurement of the
/// app's chrome is named in <c>ShellMetrics</c> ([S-6745]), never written in place.
/// </para>
/// </summary>
public sealed partial class FamilyGuardTests
{
    /// <summary>A number in code: not part of a name, not a member access.</summary>
    [GeneratedRegex(@"(?<![\w.])-?\d+(?:\.\d+)?(?![\w.])")]
    private static partial Regex Number();

    [GeneratedRegex(@"\b(FontSize|LineHeight)\s*=\s*([^;,}\r\n]+)")]
    private static partial Regex TextSize();

    [GeneratedRegex(@"\bFontWeights\.(Thin|ExtraLight|Light|SemiLight|Medium|Bold|ExtraBold|Black|ExtraBlack)\b|\bnew\s+(?:Windows\.UI\.Text\.)?FontWeight\b")]
    private static partial Regex Weight();

    [GeneratedRegex(@"\bSpacing\s*=\s*([^;,}\r\n]+)")]
    private static partial Regex Spacing();

    [GeneratedRegex(@"\bnew\s+Thickness\(([^;\r\n]*?)\)(?=\s*[,;}\r\n)])")]
    private static partial Regex Thickness();

    [GeneratedRegex(@"\bnew\s+CornerRadius\(([^;\r\n]*?)\)(?=\s*[,;}\r\n)])|\bRadius[XY]\s*=\s*([^;,}\r\n]+)")]
    private static partial Regex Radius();

    [GeneratedRegex(@"\b(MinHeight|Height|MaxHeight|MinWidth|Width|MaxWidth)\s*=\s*(-?\d+(?:\.\d+)?)\s*(?=[,;}\r\n])")]
    private static partial Regex Size();

    [GeneratedRegex(@"\bStrokeThickness\s*=\s*([^;,}\r\n]+)")]
    private static partial Regex Stroke();

    [GeneratedRegex(@"\bFluentType\.\w+|\bHeroLayout\.\w+|\bHeroFit\.(?!Ems\b)\w+|\bFluent\.(Space5|OverlayRadius|RowHeight|EmptyState)\b")]
    private static partial Regex Ramp();

    [GeneratedRegex(@"\bOpacity\s*=\s*(-?\d+(?:\.\d+)?)\s*(?=[,;}\r\n])")]
    private static partial Regex Opacity();

    [GeneratedRegex(@"(?<!,\s*)\bColors\.[A-Z]\w*|\b(?:Color|ColorHelper)\.FromArgb\(|""#[0-9A-Fa-f]{6}(?:[0-9A-Fa-f]{2})?""")]
    private static partial Regex Colour();

    [GeneratedRegex(@"""(\w+Brush)""")]
    private static partial Regex BrushKey();

    [GeneratedRegex(@"\b(ShellMetrics|ShellRoles?|ShellColours|ShellBrush)\.\w+")]
    private static partial Regex ShellToken();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    /// <summary>Every source the guard reads, as a path relative to the repository.</summary>
    private static IEnumerable<string> Sources()
    {
        static bool Kept(string root, string file)
        {
            var parts = Path.GetRelativePath(root, file).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return !parts.Any(p => p is "bin" or "obj" or "Tests" or "Fixtures" or "Gallery");
        }

        IEnumerable<string> Under(params string[] folder)
        {
            var root = RepoFiles.At(folder);
            return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories).Where(f => Kept(root, f));
        }

        return Under("src", "Widgets")
            .Concat(Under("src", "Themia.Sdk.WinUI"))
            .Concat(Under("src", "Themia.App", "Shell"))
            .Concat(Under("src", "Themia.App", "Widgets"))
            .Select(f => Path.GetRelativePath(RepoFiles.Root, f).Replace('\\', '/'))
            .Order(StringComparer.Ordinal);
    }

    /// <summary>[S-6730] Each offence of one file: rule and literal.</summary>
    internal static IEnumerable<(string Rule, string Literal)> Offences(string relative, string code)
    {
        var motion = relative.Contains("/Motion/", StringComparison.Ordinal) || relative.EndsWith("/ScrollFade.cs", StringComparison.Ordinal);

        // The one place a data colour (a user's, a calendar's) becomes a brush (HexBrush, under Family.Data): the colour rule's marked exception.
        var dataEntry = relative.EndsWith("/Shell/HexBrush.cs", StringComparison.Ordinal);

        // [S-6747] The colours that are no role and no data (a shadow's black, a backdrop's clear, the motion effects' art): their one file.
        var fixedEntry = relative.EndsWith("/Shell/ShellColours.cs", StringComparison.Ordinal);

        // [S-6747] The chrome's own tokens are the shell's: a module or one of the family's blocks may not name them (nor ShellBrush, which draws them).
        var widgetPart = relative.StartsWith("src/Widgets/", StringComparison.Ordinal) || relative.StartsWith("src/Themia.Sdk.WinUI/Kit/", StringComparison.Ordinal);

        foreach (var raw in code.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("//", StringComparison.Ordinal) || line.StartsWith('*') || line.StartsWith('#'))
            {
                continue;
            }

            foreach (Match m in TextSize().Matches(line))
            {
                if (Literal(m.Groups[2].Value, allowHairline: false))
                {
                    yield return ("type", Clean(m.Value));
                }
            }

            foreach (Match m in Weight().Matches(line))
            {
                yield return ("weight", Clean(m.Value));
            }

            foreach (Match m in Spacing().Matches(line))
            {
                if (Literal(m.Groups[1].Value, allowHairline: false))
                {
                    yield return ("space", Clean(m.Value));
                }
            }

            foreach (Match m in Thickness().Matches(line))
            {
                if (Literal(m.Groups[1].Value, allowHairline: true))
                {
                    yield return ("space", Clean(m.Value));
                }
            }

            foreach (Match m in Radius().Matches(line))
            {
                if (Literal(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value, allowHairline: false))
                {
                    yield return ("radius", Clean(m.Value));
                }
            }

            foreach (Match m in Size().Matches(line))
            {
                if (Literal(m.Groups[2].Value, allowHairline: true))
                {
                    yield return ("size", Clean(m.Value));
                }
            }

            foreach (Match m in Stroke().Matches(line))
            {
                if (Literal(m.Groups[1].Value, allowHairline: true))
                {
                    yield return ("stroke", Clean(m.Value));
                }
            }

            foreach (Match m in Ramp().Matches(line))
            {
                yield return ("ramp", Clean(m.Value));
            }

            if (!motion)
            {
                foreach (Match m in Opacity().Matches(line))
                {
                    if (Literal(m.Groups[1].Value, allowHairline: true))
                    {
                        yield return ("opacity", Clean(m.Value));
                    }
                }
            }

            foreach (Match m in Colour().Matches(line))
            {
                if (!dataEntry && !fixedEntry)
                {
                    yield return ("colour", Clean(m.Value));
                }
            }

            if (widgetPart)
            {
                foreach (Match m in ShellToken().Matches(line))
                {
                    yield return ("shell", m.Value);
                }
            }

            foreach (Match m in BrushKey().Matches(line))
            {
                if (!ColourRoles.IsRoleKey(m.Groups[1].Value))
                {
                    yield return ("brush", m.Groups[1].Value);
                }
            }
        }
    }

    /// <summary>Whether an expression holds a number other than 0 (and ±1, where a hairline is allowed).</summary>
    private static bool Literal(string expression, bool allowHairline)
        => Number().Matches(expression).Any(n => n.Value is not ("0" or "0.0" or "-0") && !(allowHairline && n.Value is "1" or "-1"));

    private static string Clean(string text) => Spaces().Replace(text.Trim(), " ");

    /// <summary>Every offence today, counted per file, rule and literal.</summary>
    private static SortedDictionary<string, int> Today()
    {
        var found = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var relative in Sources())
        {
            foreach (var (rule, literal) in Offences(relative, File.ReadAllText(RepoFiles.At(relative))))
            {
                var key = $"{relative}\t{rule}\t{literal}";
                found[key] = found.TryGetValue(key, out var n) ? n + 1 : 1;
            }
        }

        return found;
    }

    [Fact]
    public void S6730_no_literal_outside_the_family_tokens()
    {
        var found = Today();

        Assert.True(
            found.Count == 0,
            "Literals outside the family's tokens (use FamilyType, FamilySpace, FamilyShape, ColourRole; the chrome's measurements ShellMetrics, its colours ShellRole: docs/WIDGET-KIT.md 16):\n"
                + string.Join('\n', found.Select(f => $"{f.Value}× {f.Key}")));
    }

    [Fact]
    public void S6730_the_guard_sees_each_kind_of_literal_and_passes_the_tokens()
    {
        const string code = """
            text.FontSize = 18;
            text.FontSize = FamilyType.Body.FontSize;
            weight = FontWeights.Light;
            panel.Spacing = 6;
            panel.Spacing = FamilySpace.S2;
            border.Padding = new Thickness(Fluent.Space1 + 2, 0, 0, 0);
            border.BorderThickness = new Thickness(1);
            border.CornerRadius = new CornerRadius(6);
            row.Height = 40;
            path.StrokeThickness = 3;
            var step = FluentType.Display;
            var fit = HeroFit.Figures(1, 2, 3);
            var ems = HeroFit.Ems("12");
            shape.Opacity = 0.5;
            brush = new SolidColorBrush(Colors.Red);
            brush = ThemeBrush.Of("TextFillColorPrimaryBrush", Colors.White);
            brush = ThemeBrush.Of("AccentTextFillColorPrimaryBrush", Colors.White);
            var pen = "#FF8800";
            // text.FontSize = 99;
            """;

        var found = Offences("src/Widgets/Sample/Sample.cs", code).ToList();

        Assert.Equal(
            [
                ("type", "FontSize = 18"), ("weight", "FontWeights.Light"), ("space", "Spacing = 6"), ("space", "new Thickness(Fluent.Space1 + 2, 0, 0, 0)"),
                ("radius", "new CornerRadius(6)"), ("size", "Height = 40"), ("stroke", "StrokeThickness = 3"), ("ramp", "FluentType.Display"),
                ("ramp", "HeroFit.Figures"), ("opacity", "Opacity = 0.5"), ("colour", "Colors.Red"), ("brush", "AccentTextFillColorPrimaryBrush"), ("colour", "\"#FF8800\""),
            ],
            found);
    }

    [Fact]
    public void S6747_the_chromes_tokens_are_the_shells_and_its_fixed_colours_have_one_file()
    {
        const string code = """
            menu.Width = ShellMetrics.Menu.Width;
            frame.Background = ShellBrush.Of(ShellRole.Flyout);
            shadow.Color = ShellColours.Shadow;
            plate.Brush = compositor.CreateColorBrush(Colors.Black);
            button.Resources[prefix + nameof(Control.BorderBrush)] = clear;
            """;

        // In the shell: its own tokens pass; a colour made outside ShellColours does not.
        Assert.Equal([("colour", "Colors.Black")], Offences("src/Themia.App/Shell/FlyoutSurface.cs", code).ToList());

        // A widget or one of the family's blocks may not draw with the chrome's tokens.
        Assert.Equal(
            [("shell", "ShellMetrics.Menu"), ("shell", "ShellBrush.Of"), ("shell", "ShellRole.Flyout"), ("shell", "ShellColours.Shadow"), ("colour", "Colors.Black")],
            Offences("src/Widgets/Themia.Widget.Sample/SampleView.cs", code).ToList());
        Assert.Contains(("shell", "ShellRole.Flyout"), Offences("src/Themia.Sdk.WinUI/Kit/Family/RowBlock.cs", code));

        // Its one file may make a colour from numbers; nothing else may.
        Assert.Empty(Offences("src/Themia.Sdk.WinUI/Shell/ShellColours.cs", "public static Color Shadow { get; } = Color.FromArgb(0xFF, 0, 0, 0);"));
        Assert.Single(Offences("src/Themia.App/Shell/ShellColours2.cs", "public static Color Shadow { get; } = Color.FromArgb(0xFF, 0, 0, 0);"));
    }
}
