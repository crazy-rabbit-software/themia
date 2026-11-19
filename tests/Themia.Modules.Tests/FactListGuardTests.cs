using System.Text.RegularExpressions;
using Themia.Tests;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-12811] Every list of label and value lines is one component (owner, 2026-11-19: "they should reuse the same
/// base"): a fact row is built by <c>FactList</c> alone, never stacked by a widget of its own.
/// </summary>
public sealed partial class FactListGuardTests
{
    /// <summary>
    /// Where a fact row may still be made outside the list, and why: a meter's item row carries a bar (the meter's
    /// look), the sky's event row is one row of its own (sun and moon times), the stock's two lines stand under its
    /// chart (the graph widgets' own).
    /// </summary>
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "FactList.cs", "MeterView.Looks.cs", "SkyParts.cs", "StockView.cs",
    };

    [GeneratedRegex(@"new\s+RowBlock\s*\(\s*RowKind\.Fact|new\s*\(\s*RowKind\.Fact")]
    private static partial Regex FactRow();

    [Fact]
    public void S12811_no_widget_builds_fact_rows_of_its_own()
    {
        var offenders = Directory.EnumerateFiles(RepoFiles.Src, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !f.Contains($"{Path.DirectorySeparatorChar}Kit{Path.DirectorySeparatorChar}Family{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(f => !Allowed.Contains(Path.GetFileName(f)) && FactRow().IsMatch(File.ReadAllText(f)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }
}
