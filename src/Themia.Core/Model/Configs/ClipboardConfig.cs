using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// What has been copied recently.
/// <para>
/// There is deliberately <b>no persistence setting</b> here, and no field that could grow into one.
/// A clipboard history captures whatever was copied — which regularly includes a password, a
/// recovery code or a card number — so the history lives in memory for the session and is gone when
/// the app closes. Offering to write it to disk would be offering to create a credentials file, and
/// a default-off switch is still a switch somebody turns on without reading it.
/// </para>
/// <para>
/// What <i>is</i> configurable is how much is kept and how it reads, because a shorter history is
/// the one control that genuinely reduces exposure.
/// </para>
/// </summary>
public sealed class ClipboardConfig : WidgetConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Clipboard;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("clipboard"),
        Kind = WidgetKind.Clipboard,
        LabelId = Str.WidgetsA_Catalog_Clipboard_Label,
        DescriptionId = Str.WidgetsA_Catalog_Clipboard_Description,
        Glyph = Glyphs.Clipboard,
        DefaultWidth = 320,
        DefaultHeight = 260,
    };

    /// <summary>[S-1440] How many entries to keep, 1 to 200 (default 50, as the original).</summary>
    public int Capacity { get; set; } = ClipboardHistory.DefaultCapacity;

    /// <summary>Show how long ago each entry was copied.</summary>
    public bool ShowTimestamp { get; set; } = true;

    [JsonIgnore]
    public int EffectiveCapacity
        => Math.Clamp(Capacity, ClipboardHistory.MinCapacity, ClipboardHistory.MaxCapacity);

    /// <summary>
    /// The history is fed by a clipboard listener, not by polling, so this only keeps the relative
    /// timestamps ("4m", "2h") honest.
    /// </summary>
    [JsonIgnore]
    public TimeSpan TickInterval => TimeSpan.FromMinutes(1);

    public override WidgetConfig Clone() => new ClipboardConfig
    {
        Capacity = Capacity,
        ShowTimestamp = ShowTimestamp,
    };
}
