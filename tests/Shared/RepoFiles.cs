using System.Runtime.CompilerServices;

namespace Themia.Tests;

/// <summary>
/// Where the source-scanning tests find the repository's files: one locator for both test projects,
/// so a rule that reads source (a view's alignment, a page's rows, the confirmation flyout) reads
/// every place that code may live, the app, the SDK's UI parts and every widget module, and keeps
/// reading them when code moves between those places.
/// </summary>
internal static class RepoFiles
{
    /// <summary>The repository root: found from this source file, not the test binaries, which may build outside the repository.</summary>
    public static string Root { get; } = FindRoot();

    /// <summary><c>src/</c>.</summary>
    public static string Src => Path.Combine(Root, "src");

    /// <summary>A path under the root.</summary>
    public static string At(params string[] parts) => Path.Combine([Root, .. parts]);

    /// <summary>The app's widget views and their parts (<c>src/Themia.App/Widgets</c>).</summary>
    public static string AppWidgets => At("src", "Themia.App", "Widgets");

    /// <summary>The settings window's pages (<c>src/Themia.App/Shell/Config</c>).</summary>
    public static string AppConfig => At("src", "Themia.App", "Shell", "Config");

    /// <summary>The SDK's UI parts (<c>src/Themia.Sdk.WinUI</c>).</summary>
    public static string SdkWinUI => At("src", "Themia.Sdk.WinUI");

    /// <summary>Every widget module project folder (<c>src/Widgets/Themia.Widget.*</c>).</summary>
    public static IEnumerable<string> WidgetModules()
        => Directory.EnumerateDirectories(At("src", "Widgets"), "Themia.Widget.*")
            .Where(d => File.Exists(Path.Combine(d, Path.GetFileName(d) + ".csproj")));

    /// <summary>The C# sources under <paramref name="folder"/>, build output and a module's own <c>Tests/</c> left out.</summary>
    public static IEnumerable<string> CSharp(string folder)
        => Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(f =>
            {
                var relative = Path.GetRelativePath(folder, f).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return !relative.Any(p => p is "bin" or "obj" or "Tests");
            });

    /// <summary>
    /// Every source a widget view may be built from: the app's widgets, the SDK's widget parts
    /// (<c>src/Themia.Sdk.WinUI/Widgets</c>; its settings rows are the settings window's), the kit's
    /// UI parts (<c>src/Themia.Sdk.WinUI/Kit</c>) and every widget module (views, their parts and
    /// their settings rows; previews included).
    /// </summary>
    public static IEnumerable<string> ViewSources()
        => CSharp(AppWidgets)
            .Concat(CSharp(Path.Combine(SdkWinUI, "Widgets")))
            .Concat(CSharp(SdkKit))
            .Concat(WidgetModules().SelectMany(CSharp));

    /// <summary>The kit's UI parts (<c>src/Themia.Sdk.WinUI/Kit</c>).</summary>
    public static string SdkKit => Path.Combine(SdkWinUI, "Kit");

    /// <summary>
    /// A module's settings page (<c>src/Widgets/Themia.Widget.&lt;Name&gt;/&lt;Name&gt;Settings.cs</c>): the settings
    /// window's, not the widget's, though it lives in the widget's folder.
    /// </summary>
    public static bool IsSettingsPage(string path)
    {
        var folder = Path.GetDirectoryName(path);
        var module = Path.GetFileName(folder);
        return module is not null
            && module.StartsWith("Themia.Widget.", StringComparison.Ordinal)
            && string.Equals(Path.GetFileName(Path.GetDirectoryName(folder)), "Widgets", StringComparison.Ordinal)
            && string.Equals(Path.GetFileName(path), module["Themia.Widget.".Length..] + "Settings.cs", StringComparison.Ordinal);
    }

    /// <summary>The one source file named <paramref name="fileName"/> under <c>src/</c>, wherever it lives now.</summary>
    public static string Source(string fileName)
    {
        var found = CSharp(Src).Where(f => string.Equals(Path.GetFileName(f), fileName, StringComparison.Ordinal)).ToList();
        return found.Count == 1
            ? found[0]
            : throw new FileNotFoundException($"{found.Count} files named {fileName} under src/ (expected one)", fileName);
    }

    private static string FindRoot([CallerFilePath] string here = "")
    {
        for (var dir = new FileInfo(here).Directory; dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Themia.slnx")) && Directory.Exists(Path.Combine(dir.FullName, "src")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("repository root (Themia.slnx beside src/) not found above this test's source");
    }
}
