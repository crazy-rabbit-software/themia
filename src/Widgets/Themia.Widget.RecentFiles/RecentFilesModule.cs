using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.RecentFiles;

[assembly: ThemiaWidget("recentFiles", typeof(RecentFilesModule))]

namespace Themia.Widget.RecentFiles;

/// <summary>[S-4840] The files opened last, as Windows lists them in Recent Items. SPEC.md beside this file.</summary>
public sealed class RecentFilesModule : IWidgetModule
{
    /// <summary>Its glyph, the module's own (U+E81C).</summary>
    internal static readonly string Glyph = char.ConvertFromUtf32(0xE81C);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("recentFiles"),
        ConfigType = typeof(RecentFilesConfig),
        CreateDefault = () => new RecentFilesConfig(),
        Json = RecentFilesJsonContext.Default,
        Keywords = ["recent", "recently opened", "history", "documents", "files", "quick access", "last opened"],
        Label = RecentFilesStr.RecentFiles_Label.Local(),
        Description = RecentFilesStr.RecentFiles_Desc.Local(),
        Glyph = Glyph,
        DefaultWidth = 320,
        DefaultHeight = 300,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Files, 0, 2),
    };

    public FrameworkElement CreateView(IWidgetHost host) => new RecentFilesView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RecentFilesConfig))]
internal sealed partial class RecentFilesJsonContext : JsonSerializerContext;
