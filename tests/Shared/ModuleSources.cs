using System.Text.RegularExpressions;
using Themia.Sdk;
using Themia.Tests;

namespace Themia.Core.Tests;

/// <summary>
/// [S-6902] What the widget modules declare, read from their source: the source-scanning tests ask
/// which view a kind is built as, from the module projects (<c>src/Widgets/Themia.Widget.*</c>).
/// Shared by both test projects.
/// </summary>
internal static partial class ModuleSources
{
    [GeneratedRegex(@"Id = new\(""(\w+)""\)")]
    private static partial Regex IdOf();

    [GeneratedRegex(@"FrameworkElement CreateView\(IWidgetHost host\) => new (\w+)\(")]
    private static partial Regex ViewOf();

    /// <summary>The view class each kind is built as, read from its module's <c>CreateView</c>.</summary>
    public static Dictionary<WidgetId, string> Views()
    {
        var views = new Dictionary<WidgetId, string>();
        foreach (var file in Directory.EnumerateDirectories(Path.Combine(RepoFiles.Root, "src", "Widgets"), "Themia.Widget.*")
            .SelectMany(d => Directory.EnumerateFiles(d, "*Module.cs")))
        {
            var text = File.ReadAllText(file);
            if (IdOf().Match(text) is { Success: true } id && ViewOf().Match(text) is { Success: true } view)
            {
                views.Add(new WidgetId(id.Groups[1].Value), view.Groups[1].Value);
            }
        }

        return views;
    }
}
