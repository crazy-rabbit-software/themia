using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.AirQuality;

namespace Themia.Core.Model;

/// <summary>[S-5103] Which air quality index the widget reads out.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AirQualityScale>))]
public enum AirQualityScale
{
    /// <summary>The European index for a place in Europe, the US index everywhere else.</summary>
    Auto = 0,

    /// <summary>The European Environment Agency's index (EAQI), 0 to 100+.</summary>
    European = 1,

    /// <summary>The US EPA's index, 0 to 500.</summary>
    Us = 2,
}

/// <summary>[S-6169] The air widget's styles: the same reading, two looks.</summary>
[JsonConverter(typeof(StyleJsonConverter<AirQualityStyle>))]
public enum AirQualityStyle
{
    /// <summary>[S-5107] The index and its band as a number, on the ladder.</summary>
    Number = 0,

    /// <summary>[S-6170] The index on an arc of its six bands.</summary>
    Gauge = 1,
}

/// <summary>
/// [S-5100] Air quality, UV and pollen at a place. A weather-family widget: the same place picker and
/// the same header rule ([S-728]), so it derives from <see cref="WeatherConfig"/>; the sync tells the
/// kinds apart ([S-5101]).
/// </summary>
public sealed class AirQualityConfig : WeatherConfig, IStyled, IContentAligned, IInterests
{
    /// <summary>[S-2608] What the background sync keeps fresh for it.</summary>
    public void AddInterests(InterestsBuilder interests)
    {
        if (Interest(OnlineCadence.Air(interests.Refresh)) is { } wants)
        {
            interests.Of<AirInterest>().Add(wants);
        }
    }

    /// <summary>[S-5101] What it asks for, or null without a place; the running app's cadence ([S-3780]).</summary>
    public AirInterest? Interest() => Interest(OnlineCadence.Air(UpdateCadence.Current));

    /// <summary>[S-5101] <see cref="Interest()"/> at a given cadence.</summary>
    public AirInterest? Interest(TimeSpan interval)
        => WebInput.AreCoordinates(Latitude, Longitude) ? new AirInterest(Latitude!.Value, Longitude!.Value, interval) : null;

    private static readonly StyleOption[] AirStyles =
    [
        StyleOption.Of(AirQualityStyle.Number, AirQualityStr.Styles_Air_Number.Local(), 0xE75F),
        StyleOption.Of(AirQualityStyle.Gauge, AirQualityStr.Styles_Air_Gauge.Local(), 0xEC4A),
    ];


    /// <summary>[S-6581] Its one block aligns; where it sits when nothing was chosen.</summary>
    ContentAlign IContentAligned.DefaultAlign => Style == AirQualityStyle.Gauge ? ContentAlign.Center : ContentAlign.Left;

    /// <summary>[S-6169] The widget's look.</summary>
    public AirQualityStyle Style { get; set; } = AirQualityStyle.Number;

    IReadOnlyList<StyleOption> IStyled.Styles => AirStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (AirQualityStyle)value;
    }

    /// <summary>[S-5103] Auto picks by the place, not by the user's region: the index is about the air there.</summary>
    public AirQualityScale Scale { get; set; } = AirQualityScale.Auto;

    public override WidgetConfig Clone()
    {
        var copy = new AirQualityConfig { Scale = Scale, Style = Style };
        CopyTo(copy);
        return copy;
    }

    public override IReadOnlyList<string> Repair()
    {
        if (Enum.IsDefined(Scale))
        {
            return [];
        }

        var was = Scale;
        Scale = AirQualityScale.Auto;
        return [$"air quality: index {(int)was} is not one; Auto"];
    }
}
