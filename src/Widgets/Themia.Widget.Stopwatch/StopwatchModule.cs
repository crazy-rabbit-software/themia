using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Stopwatch;

[assembly: ThemiaWidget("stopwatch", typeof(StopwatchModule))]

namespace Themia.Widget.Stopwatch;

/// <summary>[S-4600] The stopwatch: the time, its controls, the laps it lists under them, running across restarts. SPEC.md beside this file.</summary>
public sealed class StopwatchModule : IWidgetModule
{
    /// <summary>Its glyph, the module's own (Segoe "Stopwatch", U+E916).</summary>
    internal static readonly string Glyph = char.ConvertFromUtf32(0xE916);

    /// <summary>[S-4601] A lap (Segoe "Flag", U+E7C1), as Windows' Clock marks one.</summary>
    internal static readonly string LapGlyph = char.ConvertFromUtf32(0xE7C1);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("stopwatch"),
        ConfigType = typeof(StopwatchConfig),
        CreateDefault = () => new StopwatchConfig(),
        Json = StopwatchJsonContext.Default,
        Keywords = ["timer", "lap", "laps", "split", "chronometer", "elapsed", "count up"],
        Label = StopwatchStr.Stopwatch_Label.Local(),
        Description = StopwatchStr.Stopwatch_Desc.Local(),
        Glyph = Glyph,
        Sizing = c => StopwatchFace.Sizing((StopwatchConfig)c, Strings.Current),
        Traits = WidgetTraits.HasItems | WidgetTraits.OneReadout,
        Add = new(AddFolderId.Time, 0, 3),
    };

    // [S-11302] [S-14410] [S-14500] No options of its own: how many laps it lists is what the card's height holds under
    // its face, how big its time is the card's size.

    public FrameworkElement CreateView(IWidgetHost host) => new StopwatchView();
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(StopwatchConfig))]
internal sealed partial class StopwatchJsonContext : JsonSerializerContext;
