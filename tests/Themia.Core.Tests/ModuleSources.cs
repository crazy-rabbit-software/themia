using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Tests;

/// <summary>
/// [S-6902] What the app's in-app widget modules (<c>src/Themia.App/Widgets/Modules</c>) declare,
/// read from their source: the source-scanning tests ask which view a kind is built as.
/// </summary>
internal static partial class ModuleSources
{
    private static string Root([CallerFilePath] string here = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", ".."));

    public static string ModulesFolder => Path.Combine(Root(), "src", "Themia.App", "Widgets", "Modules");

    [GeneratedRegex(@"WidgetDescriptor Descriptor => (\w+)\.Descriptor;")]
    private static partial Regex DescriptorOf();

    [GeneratedRegex(@"FrameworkElement CreateView\(\) => new (\w+)\(")]
    private static partial Regex ViewOf();

    /// <summary>The view class each kind is built as, read from its module's <c>CreateView</c>.</summary>
    public static Dictionary<WidgetId, string> Views()
    {
        var kindOf = TestCatalog.Declared.ToDictionary(d => d.Config.Name, d => d.Descriptor.Id, StringComparer.Ordinal);
        var views = new Dictionary<WidgetId, string>();
        foreach (var file in Directory.EnumerateFiles(ModulesFolder, "*.cs"))
        {
            var text = File.ReadAllText(file);
            if (DescriptorOf().Match(text) is { Success: true } config && ViewOf().Match(text) is { Success: true } view)
            {
                views.Add(kindOf[config.Groups[1].Value], view.Groups[1].Value);
            }
        }

        return views;
    }
}
