using System.Text.RegularExpressions;
using Themia.Tests;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// An icon is named in <c>Glyphs</c> (Shell/Glyphs.cs: a code point under a name that says what it is for), never written
/// as a raw code point where it is used: 34 were, 17 of them icons <c>Glyphs</c> already named.
/// </summary>
public sealed partial class GlyphLiteralGuardTests
{
    [Fact]
    public void Icons_are_named_in_Glyphs_never_raw_code_points()
    {
        var offenders = RepoFiles.CSharp(RepoFiles.Src)
            .Where(f => !f.EndsWith(Path.Combine("Shell", "Glyphs.cs"), StringComparison.Ordinal)
                && !f.EndsWith("FluentIconData.g.cs", StringComparison.Ordinal))
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (f, i, line)))
            .Where(x => RawCodePoint().IsMatch(x.line))
            .Select(x => $"{Path.GetRelativePath(RepoFiles.Root, x.f)}:{x.i + 1}")
            .ToList();

        Assert.True(offenders.Count == 0, "a raw icon code point; name it in Glyphs: " + string.Join(", ", offenders));
    }

    [GeneratedRegex(@"ConvertFromUtf32\(0x[EeFf][0-9A-Fa-f]{3}\)")]
    private static partial Regex RawCodePoint();
}
