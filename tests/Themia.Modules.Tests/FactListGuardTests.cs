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
    /// look), the stock's two lines stand under its chart (the graph widgets' own). A single metric's row is the list's
    /// ([S-14601]: a list of one).
    /// </summary>
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        "src/Themia.Sdk.WinUI/Widgets/FactList.cs", "src/Themia.Sdk.WinUI/Widgets/MeterView.Looks.cs", "src/Widgets/Themia.Widget.Stock/StockView.cs",
    };

    [GeneratedRegex(@"new\s+RowBlock\s*\(\s*RowKind\.Fact|new\s*\(\s*RowKind\.Fact")]
    private static partial Regex FactRow();

    [Fact]
    public void S12811_no_widget_builds_fact_rows_of_its_own()
    {
        var offenders = RepoFiles.Sources("src")
            .Where(f => !RepoFiles.Relative(f).Contains("/Tests/", StringComparison.Ordinal) && !RepoFiles.Relative(f).Contains("/Kit/Family/", StringComparison.Ordinal))
            .Where(f => FactRow().IsMatch(File.ReadAllText(f)));

        Assert.Empty(RepoFiles.AllowList(offenders, Allowed));
    }
}
