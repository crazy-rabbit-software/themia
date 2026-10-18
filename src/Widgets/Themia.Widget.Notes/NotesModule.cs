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
using Themia.Widget.Notes;

[assembly: ThemiaWidget("notes", typeof(NotesModule))]

namespace Themia.Widget.Notes;

/// <summary>[S-1346] One markdown note from the notes kept on this PC, edited in place with its formatting bar in the header. SPEC.md beside this file.</summary>
public sealed class NotesModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("notes"),
        ConfigType = typeof(NotesConfig),
        CreateDefault = () => new NotesConfig(),
        Json = NotesJsonContext.Default,
        Keywords = ["notepad", "text", "memo", "writing", "markdown", "sticky note"],
        Label = NotesStr.Widget_Notes_Label.Local(),
        Description = NotesStr.WidgetsA_Catalog_Notes_Description.Local(),
        Glyph = Glyphs.Note,
        DefaultWidth = 300,
        DefaultHeight = 220,
        Traits = WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Notes, 0, 0, Popular: 6),
    };

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => NotesSettings.Rows(rows.Rows, (NotesConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new NotesView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(NotesConfig))]
internal sealed partial class NotesJsonContext : JsonSerializerContext;
