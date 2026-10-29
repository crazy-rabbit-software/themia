using System.Text;
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
/// Today's offenders are listed in <c>FamilyGuard.allow.txt</c> beside this file (count, file, rule,
/// the literal), so the test is green on the day the guard came in; it fails on any offence not on the
/// list (a new literal, or one more of an old one) and on any entry that no longer occurs as often (a
/// fixed offender must leave the list). The list only shrinks, and every group phase ends with fewer
/// lines; the family is done when it is empty. Regenerate it (to shrink it, never to grow it) with
/// <c>THEMIA_FAMILY_GUARD_WRITE=1</c>.
/// </para>
/// </summary>
public sealed partial class FamilyGuardTests
{
    private const string AllowFile = "FamilyGuard.allow.txt";

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

    [GeneratedRegex(@"\bFluentType\.(BodyLarge|Subtitle|TitleLarge|Title|Display|Hero|FitWidth|Fit|Scaled)\b|\bHeroLayout\.\w+|\bHeroFit\.(?!Ems\b)\w+|\bFluent\.Space5\b")]
    private static partial Regex Ramp();

    [GeneratedRegex(@"\bOpacity\s*=\s*(-?\d+(?:\.\d+)?)\s*(?=[,;}\r\n])")]
    private static partial Regex Opacity();

    [GeneratedRegex(@"(?<!,\s*)\bColors\.[A-Z]\w*|\b(?:Color|ColorHelper)\.FromArgb\(|""#[0-9A-Fa-f]{6}(?:[0-9A-Fa-f]{2})?""")]
    private static partial Regex Colour();

    [GeneratedRegex(@"""(\w+Brush)""")]
    private static partial Regex BrushKey();

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
                if (!dataEntry)
                {
                    yield return ("colour", Clean(m.Value));
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

    private static string AllowPath => RepoFiles.At("tests", "Themia.Modules.Tests", AllowFile);

    private static Dictionary<string, int> Allowed()
    {
        var allowed = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(AllowPath))
        {
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var tab = line.IndexOf('\t', StringComparison.Ordinal);
            allowed[line[(tab + 1)..]] = int.Parse(line[..tab], System.Globalization.CultureInfo.InvariantCulture);
        }

        return allowed;
    }

    [Fact]
    public void S6730_no_literal_outside_the_family_tokens()
    {
        var today = Today();

        if (Environment.GetEnvironmentVariable("THEMIA_FAMILY_GUARD_WRITE") == "1")
        {
            var text = new StringBuilder();
            text.Append("# [S-6730] Today's offenders against the family's tokens: count, file, rule, literal (tab-separated).\n");
            text.Append("# The list only shrinks: FamilyGuardTests fails on an offence not listed here and on a line that no longer\n");
            text.Append("# occurs as often. Rewrite it with THEMIA_FAMILY_GUARD_WRITE=1 only after fixing offenders. Empty is done.\n");
            foreach (var (key, count) in today)
            {
                text.Append(System.Globalization.CultureInfo.InvariantCulture, $"{count}\t{key}\n");
            }

            File.WriteAllText(AllowPath, text.ToString());
            return;
        }

        var allowed = Allowed();
        var fresh = today.Where(t => t.Value > allowed.GetValueOrDefault(t.Key)).Select(t => $"{t.Value - allowed.GetValueOrDefault(t.Key)}× {t.Key}").ToList();
        var stale = allowed.Where(a => a.Value > today.GetValueOrDefault(a.Key)).Select(a => $"{a.Value - today.GetValueOrDefault(a.Key)}× fewer: {a.Key}").ToList();

        Assert.True(fresh.Count == 0, "Literals outside the family's tokens (use FamilyType, FamilySpace, FamilyShape, ColourRole: docs/WIDGET-KIT.md 16):\n" + string.Join('\n', fresh));
        Assert.True(stale.Count == 0, $"Fixed offenders still on {AllowFile} (take them off; the list only shrinks):\n" + string.Join('\n', stale));
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
}
