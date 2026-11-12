using System.Text.RegularExpressions;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Tests;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-11850]..[S-11854] The add page's pictures in every build: the samples ship by structure, the sample
/// world is handed to the picture's cards alone, a picture shows a view's own sample, the cache is
/// bounded, and the one check a shipped build runs.
/// </summary>
public sealed partial class AddPictureSamplesTests
{
    private static readonly string App = RepoFiles.At("src", "Themia.App");

    /// <summary>The kinds whose preview has no sample on purpose: an "are you sure?" only, a reading of absence, a pressed look.</summary>
    private static readonly string[] NoSample = ["ScratchpadView", "MeterView", "DetailView", "PowerButtonView"];

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

            if (name != "AddPictures.cs" && code.Contains("new SampleWorld(", StringComparison.Ordinal))
            {
                offending.Add($"{Relative(file)}: a sample world outside the add page's pictures");
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

    /// <summary>
    /// [S-11852] A picture shows a view's own sample and nothing else: no preview state is chosen by
    /// position (the meters' first state was "unavailable": the GPU and battery pictures read "--"), and
    /// it waits for its rows' icons within a bound.
    /// </summary>
    [Fact]
    public void S11852_a_picture_shows_the_views_own_sample()
    {
        var pictures = File.ReadAllText(Path.Combine(App, "Shell", "Pages", "AddPictures.cs"));
        Assert.DoesNotContain("IWidgetPreviewState", pictures, StringComparison.Ordinal);
        Assert.DoesNotContain("PreviewStates", pictures, StringComparison.Ordinal);
        Assert.DoesNotContain("#if", pictures, StringComparison.Ordinal);
        Assert.Contains("ShowSampleAsync()", pictures, StringComparison.Ordinal);
        Assert.Contains("AppIconSource.Quiet", pictures, StringComparison.Ordinal);
    }

    /// <summary>[S-11853] The cache keeps at most so many pictures, the least recently shown going first.</summary>
    [Fact]
    public void S11853_the_cache_drops_the_least_recently_shown_past_its_count()
    {
        var cache = new AddPictureCache<string>(maxCount: 2, maxBytes: 1000);
        cache.Put("a", "A", 10);
        cache.Put("b", "B", 10);
        Assert.Equal("A", cache.Get("a"));
        cache.Put("c", "C", 10);

        Assert.Equal(2, cache.Count);
        Assert.Null(cache.Get("b"));
        Assert.Equal("A", cache.Get("a"));
        Assert.Equal("C", cache.Get("c"));
        Assert.Equal(20, cache.Bytes);
    }

    /// <summary>[S-11853] ... and at most so many bytes; one picture larger than all of them is not kept.</summary>
    [Fact]
    public void S11853_the_cache_keeps_within_its_bytes()
    {
        var cache = new AddPictureCache<string>(maxCount: 10, maxBytes: 100);
        cache.Put("a", "A", 40);
        cache.Put("b", "B", 40);
        cache.Put("c", "C", 40);
        Assert.Null(cache.Get("a"));
        Assert.Equal(80, cache.Bytes);

        cache.Put("huge", "H", 101);
        Assert.Null(cache.Get("huge"));
        Assert.Equal(2, cache.Count);

        cache.Put("b", "B2", 10);
        Assert.Equal("B2", cache.Get("b"));
        Assert.Equal(50, cache.Bytes);

        cache.Clear();
        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.Bytes);
    }

    /// <summary>[S-11853] The page's bound: every tab's tiles fit, and the bytes stay tens of megabytes.</summary>
    [Fact]
    public void S11853_the_add_page_bounds_its_pictures()
    {
        var pictures = File.ReadAllText(Path.Combine(App, "Shell", "Pages", "AddPictures.cs"));
        Assert.Contains("AddPictureCache<AddPicture> Cache = new(MaxPictures, MaxBytes);", pictures, StringComparison.Ordinal);
        Assert.Matches(@"MaxBytes = \d+L \* 1024 \* 1024;", pictures);
        Assert.Contains("Cache.Clear();", pictures, StringComparison.Ordinal);
    }

    /// <summary>
    /// [S-11854] A shipped build knows one flag besides the app's own, the add page's check, so it is
    /// never refused as unknown and never reaches the hand-off; Debug knows it too, with its renders.
    /// </summary>
    [Fact]
    public void S11854_the_check_is_known_in_every_build()
    {
        var app = File.ReadAllText(Path.Combine(App, "App.xaml.cs"));
        Assert.Contains("[Shell.AddPicturesCheck.Flag, .. Shell.Gallery.RenderFlags.All];", app, StringComparison.Ordinal);
        Assert.Contains("#else\n        [Shell.AddPicturesCheck.Flag];", app.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Equal(StartRequest.Render, StartArguments.Classify(["Themia.exe", "--add-pictures-check", "x.png"], ["--add-pictures-check"]).Request);

        var check = File.ReadAllText(Path.Combine(App, "Shell", "Pages", "AddPicturesCheck.cs"));
        Assert.Contains("public const string Flag = \"--add-pictures-check\";", check, StringComparison.Ordinal);
        Assert.Contains("!string.Equals(args[1], Flag, StringComparison.Ordinal)", check, StringComparison.Ordinal);
    }
}
