using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// A live camera picture: an MJPEG stream, a snapshot endpoint polled on an interval, or a video
/// stream Windows can play itself.
/// <para>
/// The address comes from a text box and from the hand-editable state file and is handed to a
/// socket, so it is checked by <see cref="WebInput.Url"/> on every read — see
/// <see cref="ResolvedUrl"/>. Nothing else in the widget re-checks the scheme.
/// </para>
/// </summary>
public sealed class VideoStreamConfig : WidgetConfig, ISetupRule
{
    /// <summary>[S-6906] It waits for setup while this holds.</summary>
    public bool NeedsSetup(FeatureSet signedIn) => ResolvedUrl.Problem == WebUrlProblem.Empty;

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
