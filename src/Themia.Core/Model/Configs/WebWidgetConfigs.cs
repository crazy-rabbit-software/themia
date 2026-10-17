using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>[S-466] How a video picture fills the widget. Named for what it does to the picture.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StreamFit>))]
public enum StreamFit
{
    /// <summary>The whole picture, letterboxed. The default: a camera view that crops is a camera view that hides things.</summary>
    Contain = 0,

    /// <summary>Fill the widget and crop what does not fit.</summary>
    Cover = 1,

    /// <summary>Fill both axes and distort.</summary>
    Stretch = 2,
}

/// <summary>[S-468] Clockwise rotation for a camera mounted sideways or upside down. A closed set.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StreamRotation>))]
public enum StreamRotation
{
    None = 0,
    Quarter = 90,
    Half = 180,
    ThreeQuarter = 270,
}

/// <summary>
/// A live camera picture: an MJPEG stream, a snapshot endpoint polled on an interval, or a video
/// stream Windows can play itself.
/// <para>
/// The address comes from a text box and from the hand-editable state file and is handed to a
/// socket, so it is checked by <see cref="WebInput.Url"/> on every read — see
/// <see cref="ResolvedUrl"/>. Nothing else in the widget re-checks the scheme.
/// </para>
/// </summary>
public sealed class VideoStreamConfig : WidgetConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.VideoStream;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("videoStream"),
        Kind = WidgetKind.VideoStream,
        ConfigType = typeof(VideoStreamConfig),
        CreateDefault = () => new VideoStreamConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["camera", "webcam", "cctv", "mjpeg", "live view", "printer"],
        LabelId = Str.WidgetsA_Catalog_VideoStream_Label,
        DescriptionId = Str.WidgetsA_Catalog_VideoStream_Description,
        Glyph = Glyphs.Video,
        DefaultWidth = 320,
        DefaultHeight = 240,
        Traits = WidgetTraits.CanHaveHeader,
    };

    /// <summary>The stream address, exactly as typed. Never repaired; never normalised, because cameras route on the query.</summary>
    public string Url { get; set; } = string.Empty;

    public StreamFit Fit { get; set; } = StreamFit.Contain;

    public StreamRotation Rotation { get; set; } = StreamRotation.None;

    /// <summary>[S-469] Stopped by the user. Persisted, so a parked camera stays parked across restarts.</summary>
    public bool Paused { get; set; }

    /// <summary>[S-470] 0 holds the stream open; anything else grabs one frame that often and hangs up.</summary>
    public int RefreshSeconds { get; set; }

    /// <summary>
    /// [S-1312] A persistent status chip (LIVE, reconnecting, "every 10 s", the transport) at the
    /// largest size. Off by default: the original was the picture and nothing else.
    /// </summary>
    public bool ShowStatus { get; set; }

    /// <summary>
    /// [S-1312] On pointer-over, a strip with the stream's state and age (from M) and Pause, Rotate
    /// and Refresh with the resolution and frame rate (from L). On by default: it is invisible until
    /// the pointer is on the picture, and never at the small sizes.
    /// </summary>
    public bool HoverControls { get; set; } = true;

    /// <summary>The checked address, or why it was refused. Read on every use, never cached.</summary>
    [JsonIgnore]
    public WebUrlResult ResolvedUrl => WebInput.Url(Url);

    [JsonIgnore]
    public int EffectiveRefreshSeconds => VideoStreamRules.ClampRefreshSeconds(RefreshSeconds);

    /// <summary>A rotation the file names but the enum does not is read as no rotation, never as 45 degrees.</summary>
    [JsonIgnore]
    public StreamRotation EffectiveRotation => Enum.IsDefined(Rotation) ? Rotation : StreamRotation.None;

    [JsonIgnore]
    public StreamFit EffectiveFit => Enum.IsDefined(Fit) ? Fit : StreamFit.Contain;

    public override WidgetConfig Clone() => new VideoStreamConfig
    {
        Url = Url,
        Fit = Fit,
        Rotation = Rotation,
        Paused = Paused,
        RefreshSeconds = RefreshSeconds,
        ShowStatus = ShowStatus,
        HoverControls = HoverControls,
    };
}
