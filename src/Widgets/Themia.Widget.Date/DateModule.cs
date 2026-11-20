using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.Date;
using Themia.App.Shell;

[assembly: ThemiaWidget("date", typeof(DateModule))]

namespace Themia.Widget.Date;

/// <summary>
/// [S-10800] Today's date, a widget of its own (owner, 2026-11-17: "the clock widget should not have a date style
/// as this should be moved into its own widget"). Its behaviour is SPEC.md beside this file.
/// </summary>
public sealed class DateModule : IWidgetModule
{
    /// <summary>Its glyph (Segoe Fluent Icons U+E787, Calendar day), the module's own.</summary>
    internal static readonly string Glyph = Glyphs.Calendar;

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("date"),
        ConfigType = typeof(DateConfig),
        CreateDefault = () => new DateConfig(),
        Json = DateJsonContext.Default,
        Keywords = ["date", "day", "today", "weekday", "month", "calendar page"],
        Label = DateStr.Date_Label.Local(),
        Description = DateStr.Date_Desc.Local(),
        Glyph = Glyph,
        Sizing = config => DateLayout.Sizing(config as DateConfig ?? new DateConfig(), Strings.Current),
        Traits = WidgetTraits.OneReadout,
        // [S-6907] Beside the clock, among the time's widgets.
        Add = new(AddFolderId.Time, 0, 4),
    };

    /// <remarks>[S-10802] No lines of its own beside the style: how big the date is is the card's ([S-2449] retired).</remarks>
    public FrameworkElement CreateView(IWidgetHost host) => new DateView();
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DateConfig))]
internal sealed partial class DateJsonContext : JsonSerializerContext;
