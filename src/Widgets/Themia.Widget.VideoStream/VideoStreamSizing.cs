using System.Text.Json.Serialization;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Widget.VideoStream;

/// <summary>[S-11208] The picture's shape the card keeps: a camera's usual ones, or none.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StreamAspect>))]
public enum StreamAspect
{
    /// <summary>16:9, most cameras' and streams'.</summary>
    Wide = 0,

    /// <summary>4:3, older and many indoor cameras'.</summary>
    Standard = 1,

    /// <summary>1:1.</summary>
    Square = 2,

    /// <summary>No shape kept: a free width and height.</summary>
    Free = 3,
}

/// <summary>
/// [S-11208] [S-14500] The camera's sizes: with an aspect, a new card and its least in the stream's shape (a quarter turn
/// turns the shape too), 320 wide and 160 at least; with none, a free range 160..800 × 120..600, a new one 320 × 240. Both
/// axes take every grid cell (owner, 2026-11-20: "lets not limit the user in the alignment"): a card of another shape
/// shows the picture as its Fit says (whole, cropped or stretched).
/// </summary>
public static class VideoStreamSizing
{
    public const double LeastWidth = 160;

    public static readonly SizeDip Least = new(160, 120), Natural = new(320, 240), Largest = new(800, 600);

    /// <summary>[S-11208] Width over height of <paramref name="aspect"/>, turned by <paramref name="rotation"/>; null for Free.</summary>
    public static double? Ratio(StreamAspect aspect, StreamRotation rotation)
    {
        double? ratio = aspect switch
        {
            StreamAspect.Wide => 16.0 / 9,
            StreamAspect.Standard => 4.0 / 3,
            StreamAspect.Square => 1,
            _ => null,
        };

        return ratio is { } r && rotation is StreamRotation.Quarter or StreamRotation.ThreeQuarter ? 1 / r : ratio;
    }

    public static WidgetSizing Sizing(VideoStreamConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (Ratio(config.EffectiveAspect, config.EffectiveRotation) is not { } ratio)
        {
            return WidgetSizing.Range(Least, Natural, Largest) with { Room = StretchRoom.Picture };
        }

        // [S-11208] The shape at a width, never taller than the largest: a tall shape's least and natural are as wide as
        // 600 high allows.
        SizeDip Shaped(double width) => new(Math.Min(width, Largest.Height * ratio), Math.Min(width / ratio, Largest.Height));
        return WidgetSizing.Range(Shaped(LeastWidth), Shaped(Natural.Width), Largest) with { Room = StretchRoom.Picture };
    }
}
