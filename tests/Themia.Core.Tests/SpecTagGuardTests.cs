using System.Text.RegularExpressions;
using Themia.Tests;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// A test named after a SPEC tag (<c>S512_…</c>) cites a live rule: one SPEC.md (the root's or a module's) defines
/// (<c>- **[S-512]**</c>) and has not retired. 43 tags were cited that no SPEC defines and 27 that are retired; they are
/// listed here, shrink only: re-home such a test under its live tag (or drop the tag) and take it off the list.
/// </summary>
public sealed partial class SpecTagGuardTests
{
    /// <summary>Today's offenders, shrink only.</summary>
    private static readonly HashSet<int> Known = [102, 105, 220, 221, 222, 250, 251, 252, 253, 254, 303, 304, 305, 330, 331, 332, 333, 334, 335, 451, 499, 614, 659, 667, 704, 728, 782, 836, 1068, 1173, 1401, 1402, 1403, 1404, 1405, 1406, 1407, 1408, 1409, 1411, 1447, 1706, 1708, 2260, 2449, 3312, 3532, 3543, 3584, 3718, 3736, 3738, 3748, 3821, 4629, 4800, 5555, 5560, 5561, 5562, 5563, 5570, 5580, 5581, 6270, 7031, 9918, 11931, 11941];

    [Fact]
    public void Test_names_cite_live_spec_tags()
    {
        var (defined, retired) = Tags();
        var cited = TestMethods().ToList();

        var offending = cited.Where(c => !defined.Contains(c.Tag) || retired.Contains(c.Tag)).ToList();
        var fresh = offending.Where(c => !Known.Contains(c.Tag)).Select(c => $"{c.Name} (S-{c.Tag}: {(defined.Contains(c.Tag) ? "retired" : "no SPEC defines it")})").Distinct().ToList();
        Assert.True(fresh.Count == 0, "a test cites a tag that is not a live rule: " + string.Join(", ", fresh));

        // Shrink only: a tag that no longer offends comes off the list.
        var stale = Known.Where(k => !offending.Any(c => c.Tag == k)).ToList();
        Assert.True(stale.Count == 0, "no longer cited by a test or live again; take it off Known: " + string.Join(", ", stale));
    }

    private static (HashSet<int> Defined, HashSet<int> Retired) Tags()
    {
        var defined = new HashSet<int>();
        var retired = new HashSet<int>();
        var specs = new[] { RepoFiles.At("SPEC.md") }.Concat(RepoFiles.WidgetModules().Select(m => Path.Combine(m, "SPEC.md")).Where(File.Exists));
        foreach (var line in specs.SelectMany(File.ReadLines))
        {
            if (Definition().Match(line) is { Success: true } m)
            {
                var tag = int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                defined.Add(tag);
                if (m.Groups[2].Value.TrimStart().StartsWith("*(Retired", StringComparison.Ordinal))
                {
                    retired.Add(tag);
                }
            }
        }

        return (defined, retired);
    }

    private static IEnumerable<(string Name, int Tag)> TestMethods()
    {
        var files = RepoFiles.Sources("tests")
            .Concat(RepoFiles.Sources("src", "Widgets").Where(f => RepoFiles.Relative(f).Contains("/Tests/", StringComparison.Ordinal)));
        foreach (var file in files)
        {
            foreach (Match m in TestName().Matches(File.ReadAllText(file)))
            {
                yield return (m.Groups[1].Value, int.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));
            }
        }
    }

    [GeneratedRegex(@"^\s*- \*\*\[S-(\d+)\]\*\*(.*)$")]
    private static partial Regex Definition();

    [GeneratedRegex(@"\b(?:public|internal)\s+(?:async\s+)?(?:void|Task)\s+(S(\d+)_\w*)")]
    private static partial Regex TestName();
}
