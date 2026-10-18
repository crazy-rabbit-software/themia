using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Services.Web;

/// <summary>Where a stream connection stands, as the widget shows it.</summary>
public enum StreamPhase
{
    /// <summary>A connection is open and no frame has decoded yet.</summary>
    Connecting,

    /// <summary>At least one frame is on screen.</summary>
    Live,

    /// <summary>The last attempt failed and a retry is armed.</summary>
    Retrying,

    /// <summary>The address answered with something this widget cannot show. Not retried.</summary>
    Unsupported,
}

/// <summary>The size the picture will be drawn at, so frames are decoded no larger than that.</summary>
/// <param name="Width">Physical pixels across the frame area.</param>
/// <param name="Height">Physical pixels down the frame area.</param>
public readonly record struct DecodeTarget(int Width, int Height, StreamFit Fit, bool Sideways);

/// <summary>
/// [S-1313] What came with a decoded frame: the camera's own size (before the decode scaled it to
/// the tile) and how it arrived, for the camera's status chip and hover strip.
/// </summary>
public readonly record struct FrameFacts(int SourceWidth, int SourceHeight, StreamTransport Transport);
