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
using Themia.Widget.FocusSession;

[assembly: ThemiaWidget("focusSession", typeof(FocusSessionModule))]

namespace Themia.Widget.FocusSession;

/// <summary>[S-4420] The app's one focus session: a countdown that holds Windows' do not disturb while it runs, started and shown by any focus widget. SPEC.md beside this file.</summary>
public sealed class FocusSessionModule : IWidgetModule
{
    /// <summary>Its glyph, the module's own (U+F272).</summary>
    internal static readonly string Glyph = char.ConvertFromUtf32(0xF272);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("focusSession"),
        ConfigType = typeof(FocusSessionConfig),
        CreateDefault = () => new FocusSessionConfig(),
        Json = FocusSessionJsonContext.Default,
        Keywords = ["focus", "focus assist", "do not disturb", "dnd", "timer", "deep work", "concentrate"],
        Label = FocusSessionStr.Panels_Focus_Label.Local(),
        Description = FocusSessionStr.Panels_Focus_Desc.Local(),
        Glyph = Glyph,
        DefaultWidth = 240,
        DefaultHeight = 200,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Time, 1, 1),
    };

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => FocusSessionSettings.Rows(rows.Rows, (FocusSessionConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new FocusSessionView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(FocusSessionConfig))]
internal sealed partial class FocusSessionJsonContext : JsonSerializerContext;
