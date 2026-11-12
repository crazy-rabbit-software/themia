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
/// [S-11208] The camera's sizes (headerless, its default): with an aspect, the width in steps of 32
/// from 160 (the config's <see cref="VideoStreamConfig.Span"/>) and the height following it, so the
/// picture keeps the stream's shape (a quarter turn turns the shape too); with none, a free range
/// 160..800 × 120..600. Never taller than 600 nor wider than 800.
/// </summary>
public static class VideoStreamSizing
{
    public const double LeastWidth = 160;

    public const double Step = 32;

    public static readonly SizeDip Least = new(160, 120), Natural = new(320, 240), Largest = new(800, 600);

    /// <summary>A new camera's width steps: 160 + 5 × 32 = 320.</summary>
    public const int DefaultSpan = 5;

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

    /// <summary>[S-11208] The most width steps a shape allows: within 800 wide and 600 high.</summary>
    public static int MaxSpan(double ratio)
        => Math.Max(0, (int)Math.Floor((Math.Min(Largest.Width, Largest.Height * ratio) - LeastWidth) / Step));

    /// <summary>[S-11208] The width steps, the config's span held to its shape's range.</summary>
    public static SizeSteps Steps(double ratio, int span)
        => new(LeastWidth, Step, 0, MaxSpan(ratio), Math.Clamp(span, 0, MaxSpan(ratio)));

    public static WidgetSizing Sizing(VideoStreamConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (Ratio(config.EffectiveAspect, config.EffectiveRotation) is not { } ratio)
        {
            return WidgetSizing.Range(Least, Natural, Largest);
        }

        var steps = Steps(ratio, config.Span);
        var height = steps.At(steps.Count!.Value) / ratio;
        return WidgetSizing.Range(new(1, height), new(1, height), new(1, height)).WithWidthSteps(steps);
    }

    /// <summary>[S-11208] The span a width means for a shaped picture; a copy when it changes, else the same config.</summary>
    public static WidgetConfig SizeSets(VideoStreamConfig config, SizeDip size)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (Ratio(config.EffectiveAspect, config.EffectiveRotation) is not { } ratio)
        {
            return config;
        }

        var span = Steps(ratio, config.Span).Nearest(size.Width);
        if (span == config.Span)
        {
            return config;
        }

        var copy = (VideoStreamConfig)config.Clone();
        copy.Span = span;
        return copy;
    }
}
