using System.Text.RegularExpressions;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Tests;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-11850] [S-11851] The widgets' samples in every build (the widget gallery's cards, the Appearance's preview): they ship
/// by structure and the sample world is handed to the preview's cards alone. ([S-11852]..[S-11854], the add page's pictures,
/// went with the add page, [S-16728].)
/// </summary>
public sealed partial class AddPictureSamplesTests
{
    private static readonly string App = RepoFiles.At("src", "Themia.App");

    /// <summary>
    /// The kinds whose preview has no sample on purpose: an "are you sure?" only, a reading of absence, a pressed look; and the
    /// single metrics' base (<c>MetricView</c>, [S-14605]), whose families ship their own samples or draw the sample PC.
    /// </summary>
    private static readonly string[] NoSample = ["ScratchpadView", "MeterView", "DetailView", "PowerButtonView", "MetricView", "TextView", "IconView"];

    private static string Relative(string file) => Path.GetRelativePath(RepoFiles.Root, file).Replace('\\', '/');

    private static bool Under(string file, string folder)
        => file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains(folder, StringComparer.Ordinal);

    [GeneratedRegex(@"partial class (\w+)\s*:\s*IWidgetPreviewState")]
    private static partial Regex PreviewView();

    [GeneratedRegex(@"partial class (\w+)\s*:\s*IWidgetSample")]
    private static partial Regex SampleView();

    [GeneratedRegex(@"AppServices\.(System|Gallery)\s*=[^=]")]
    private static partial Regex StaticSwap();

    /// <summary>
    /// [S-11850] One rule, by structure: a module's <c>Samples/</c> ships (no <c>#if</c>, nothing of the
    /// Debug render tooling), its <c>Preview/</c> is removed outside Debug, and nothing outside
    /// <c>Preview/</c> names a preview state, so a Release build compiles without it.
    /// </summary>
    [Fact]
    public void S11850_samples_ship_and_previews_stay_debug_by_folder()
    {
        var targets = File.ReadAllText(RepoFiles.At("src", "Widgets", "Directory.Build.targets"));
        Assert.Contains(@"<Compile Remove=""Preview\**"" Condition=""'$(Configuration)' != 'Debug'"" />", targets, StringComparison.Ordinal);
        Assert.DoesNotContain(@"Remove=""Samples", targets, StringComparison.Ordinal);

        var offending = new List<string>();
        foreach (var module in RepoFiles.WidgetModules())
        {
            foreach (var file in RepoFiles.CSharp(module))
            {
                var code = File.ReadAllText(file);
                if (Under(file, "Samples") && (code.Contains("#if", StringComparison.Ordinal) || code.Contains("Shell.Gallery", StringComparison.Ordinal) || code.Contains("IWidgetPreviewState", StringComparison.Ordinal)))
                {
                    offending.Add($"{Relative(file)}: a sample ships; it names no Debug code and has no #if");
                }

                if (!Under(file, "Preview") && code.Contains("IWidgetPreviewState", StringComparison.Ordinal))
                {
                    offending.Add($"{Relative(file)}: a preview state outside Preview/");
                }

                // The folder decides what is Debug only: no #if in a module, its Preview/ included (64 previews and
                // four views carried one; a Debug-only member lives in a Preview/ partial).
                if (code.Contains("#if", StringComparison.Ordinal))
                {
                    offending.Add($"{Relative(file)}: an #if; Debug-only code goes in Preview/");
                }
            }
        }

        Assert.Empty(offending);
    }

    /// <summary>
    /// [S-11850] A kind with a sample and render states names its sample's state "sample" ("their sample state calls the
    /// sample"): 17 named it after what it showed ("quote", "documents", "tasks"), so `--state sample` failed on them.
    /// </summary>
    [Fact]
    public void S11850_the_sample_state_is_named_sample()
    {
        var offending = new List<string>();
        foreach (var module in RepoFiles.WidgetModules())
        {
            var files = RepoFiles.CSharp(module).ToList();
            if (!files.Any(f => Under(f, "Samples")))
            {
                continue;
            }

            foreach (var preview in files.Where(f => Under(f, "Preview")))
            {
                var code = File.ReadAllText(preview);
                if (code.Contains("PreviewStates", StringComparison.Ordinal)
                    && !code.Contains("\"sample\"", StringComparison.Ordinal)
                    && !code.Contains("SampleScreenTime.States", StringComparison.Ordinal))
                {
                    offending.Add(Relative(preview));
                }
            }
        }

        Assert.Empty(offending);
    }

    /// <summary>
    /// [S-11850] Every view a render can show in a state has its sample, shipped, so the add page draws
    /// it in every build; only the kinds that draw the sample world as it is (the meters and details over
    /// the sample PC) or whose states are not data (an "are you sure?", a pressed look) have none.
    /// </summary>
    [Fact]
    public void S11850_every_previewed_view_has_a_shipped_sample()
    {
        var sources = RepoFiles.CSharp(RepoFiles.Src).Select(f => (File: f, Code: File.ReadAllText(f))).ToList();
        var previewed = sources.SelectMany(s => PreviewView().Matches(s.Code).Select(m => m.Groups[1].Value)).ToHashSet(StringComparer.Ordinal);
        var sampled = sources.Where(s => Under(s.File, "Samples")).SelectMany(s => SampleView().Matches(s.Code).Select(m => m.Groups[1].Value)).ToHashSet(StringComparer.Ordinal);

        Assert.Empty(previewed.Except(sampled).Except(NoSample).Order(StringComparer.Ordinal));
        Assert.Empty(sampled.Intersect(NoSample));
    }

    /// <summary>
    /// [S-11851] The sample world reaches only the cards built for pictures: it is handed to their host
    /// (<c>PreviewCardHost.Samples</c>) and read through it, never installed in a static a desktop card
    /// reads. Outside the app's start (<c>AppHost</c>) and the Debug render tooling (<c>Shell/Gallery</c>,
    /// which runs instead of the app), nothing assigns the hub or the gallery service.
    /// </summary>
    [Fact]
    public void S11851_the_sample_world_is_handed_never_installed()
    {
        var offending = new List<string>();
        var tooling = $"{Path.DirectorySeparatorChar}Shell{Path.DirectorySeparatorChar}Gallery{Path.DirectorySeparatorChar}";
        foreach (var file in RepoFiles.CSharp(RepoFiles.Src).Where(f => !f.Contains(tooling, StringComparison.Ordinal)))
        {
            var code = File.ReadAllText(file);
            var name = Path.GetFileName(file);
            if (name != "AppHost.cs" && StaticSwap().IsMatch(code))
            {
                offending.Add($"{Relative(file)}: swaps a static the desktop's cards read");
            }

            if (name is not ("SampleWorld.cs" or "SampleMachine.cs") && code.Contains("SampleMachine.Hub(", StringComparison.Ordinal))
            {
                offending.Add($"{Relative(file)}: builds the sample PC's hub outside the sample world");
            }

            // [S-2311] The Appearance's preview is a picture of cards on sample data too, its world handed to its host.
            // [S-14912] The widget gallery's cards too.
            if (name is not ("AppearancePreview.cs" or "WidgetGalleryView.cs") && code.Contains("new SampleWorld(", StringComparison.Ordinal))
            {
                offending.Add($"{Relative(file)}: a sample world outside the Appearance's preview and the widget gallery");
            }
        }

        Assert.Empty(offending);

        // The card's host decides, explicitly: the desktop's surface has no samples, a card reads its host's.
        Assert.DoesNotContain("Samples", File.ReadAllText(Path.Combine(App, "Shell", "DesktopWindow.cs")), StringComparison.Ordinal);
        var card = File.ReadAllText(Path.Combine(App, "Shell", "CardWidgetHost.cs"));
        Assert.Contains("ISystemFeeds? System => card.Samples is { } samples ? samples.System : AppServices.System;", card, StringComparison.Ordinal);
        Assert.Contains("IGalleries? Gallery => card.Samples is { } samples ? samples.Gallery : AppServices.Gallery;", card, StringComparison.Ordinal);
        Assert.Contains("CopyText(string text) => !card.IsPreview && ", card, StringComparison.Ordinal);

        // A preview card never reads the user's accounts: the recordings in Debug, a sync with none otherwise.
        var services = File.ReadAllText(Path.Combine(App, "Services", "Kit", "HostServiceSources.cs"));
        Assert.Matches(@"if \(card\.IsPreview\)\s*\{\s*#if DEBUG\s*return Shell\.Gallery\.FixtureServices\.For\(card\);\s*#else\s*return new Detached\(card\.Ticker\);\s*#endif\s*\}", services);
    }
}
