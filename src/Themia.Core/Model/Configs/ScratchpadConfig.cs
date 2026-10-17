using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// One stroke of ink: where it went, how thick it was and what colour it was.
/// <para>
/// A mutable class rather than a record struct because this is what is serialised, and the JSON
/// source generator wants a property bag with a parameterless constructor.
/// </para>
/// </summary>
public sealed class InkStroke
{
    /// <summary>The ink colour, as <c>#RRGGBB</c>. This one is content, not chrome: it is what was drawn.</summary>
    public string Color { get; set; } = string.Empty;

    /// <summary>The pen width in device-independent pixels. See <see cref="ScratchpadConfig"/> for why not normalised.</summary>
    public double Width { get; set; } = InkGeometry.DefaultWidth;

    /// <summary>The path, flattened to x, y, x, y in the pad's unit square. See <see cref="InkGeometry.Pack"/>.</summary>
    public List<double> Points { get; set; } = [];

    public InkStroke Clone() => new()
    {
        Color = Color,
        Width = Width,
        Points = [.. Points],
    };
}

/// <summary>
/// A pad to draw on, kept as strokes rather than as a picture.
/// <para>
/// <b>Why geometry and not a bitmap.</b> The original web widget serialised the canvas to a base64
/// PNG and put that string in its config. Here that would be wrong three times over. The state file
/// is a single document written atomically by one writer, so a few hundred kilobytes of base64
/// would be rewritten in full every time <i>any</i> widget anywhere saved — dragging a clock would
/// rewrite the sketch. A bitmap is also fixed to the size it was captured at, so every resize
/// resamples it and the drawing goes soft, while strokes are redrawn at whatever size the widget
/// now is and stay crisp. And strokes can be undone, erased individually and inspected in the file;
/// a PNG can only be replaced.
/// </para>
/// <para>
/// The <i>path</i> is normalised to the pad's unit square so it scales with the widget; the pen
/// <i>width</i> stays in pixels, because a pen has a nib and a wider widget should show more
/// drawing rather than a fatter line.
/// </para>
/// </summary>
public sealed class ScratchpadConfig : WidgetConfig
{
    /// <summary>The pen a new pad starts with: the Fluent accent, which is also the app's own blue.</summary>
    public const string DefaultPenColor = "#0078D4";

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Scratchpad;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("scratchpad"),
        Kind = WidgetKind.Scratchpad,
        ConfigType = typeof(ScratchpadConfig),
        CreateDefault = () => new ScratchpadConfig(),
        Json = WidgetConfigJsonContext.Default,
        LabelId = Str.Widget_Scratchpad_Label,
        DescriptionId = Str.WidgetsA_Catalog_Scratchpad_Description,
        Glyph = Glyphs.Scratchpad,
        DefaultWidth = 300,
        DefaultHeight = 260,
    };

    /// <summary>The drawing, oldest stroke first. Drawing order is paint order.</summary>
    public List<InkStroke> Strokes { get; set; } = [];

    /// <summary>The ink colour, as <c>#RRGGBB</c>.</summary>
    public string PenColor { get; set; } = DefaultPenColor;

    /// <summary>The pen width in device-independent pixels.</summary>
    public double PenWidth { get; set; } = InkGeometry.DefaultWidth;

    /// <summary>Whether the pen, eraser, undo and clear commands sit in the widget's header.</summary>
    public bool ShowTools { get; set; } = true;

    /// <summary>
    /// The pen colour as something that can actually be drawn with. A colour that no longer parses
    /// — a hand-edited file, a half-typed value in settings — falls back rather than throwing on
    /// the render path.
    /// </summary>
    [JsonIgnore]
    public string EffectivePenColor
    {
        get
        {
            var normalised = HexColor.Normalise(PenColor);
            return normalised.Length > 0 ? normalised : DefaultPenColor;
        }
    }

    /// <summary>The pen width, clamped to what the pad can draw.</summary>
    [JsonIgnore]
    public double EffectivePenWidth => InkGeometry.ClampWidth(PenWidth);

    public override WidgetConfig Clone()
    {
        var copy = new ScratchpadConfig
        {
            PenColor = PenColor,
            PenWidth = PenWidth,
            ShowTools = ShowTools,
        };

        // A deep copy: a duplicated pad must not share a stroke list with the one it came from, or
        // drawing on one would draw on both.
        foreach (var stroke in Strokes)
        {
            copy.Strokes.Add(stroke.Clone());
        }

        return copy;
    }
}
