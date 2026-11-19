using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Widget.RainGraph;

/// <summary>[S-10201] The rain graph's looks: the hours' amounts as columns, or the next two hours' quarters (the nowcast).</summary>
[JsonConverter(typeof(StyleJsonConverter<RainGraphStyle>))]
public enum RainGraphStyle
{
    /// <summary>A column per hour over the window, the chance as a line.</summary>
    Hours = 0,

    /// <summary>[S-10203] A column per quarter hour for the next two hours: the rain widget's nowcast ([S-5122]).</summary>
    Nowcast = 1,
}

/// <summary>[S-10200] A rain graph: a place, its window, its look (the hours or the nowcast), its chart look ([S-6752]: the bars by default) and whether the chance line is drawn.</summary>
public sealed class RainGraphConfig : HourGraphConfig, IStyled
{
    private static readonly StyleOption[] Looks =
    [
        StyleOption.Of(RainGraphStyle.Hours, RainGraphStr.RainGraph_Style_Hours.Local(), 0xE9D2),
        StyleOption.Of(RainGraphStyle.Nowcast, RainGraphStr.RainGraph_Style_Nowcast.Local(), 0xEA91),
    ];

    public RainGraphConfig()
        : base(ChartType.Bars)
    {
    }

    public RainGraphStyle Style { get; set; } = RainGraphStyle.Hours;

    /// <summary>[S-10202] Each hour's chance of precipitation as the graph's second, tertiary line.</summary>
    public bool ShowChance { get; set; } = true;

    IReadOnlyList<StyleOption> IStyled.Styles => Looks;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (RainGraphStyle)value;
    }

    /// <summary>[S-10203] The nowcast asks for the rain's 15-minute answer, the hours for the hourly one.</summary>
    public override void AddInterests(InterestsBuilder interests)
    {
        ArgumentNullException.ThrowIfNull(interests);
        if (Style != RainGraphStyle.Nowcast)
        {
            base.AddInterests(interests);
        }
        else if (Nowcast(OnlineCadence.Rain(interests.Refresh)) is { } wants)
        {
            interests.Of<RainInterest>().Add(wants);
        }
    }

    /// <summary>[S-10203] The nowcast's want at the running app's cadence, or null without a place or in the hours look.</summary>
    public RainInterest? Nowcast() => Nowcast(OnlineCadence.Rain(UpdateCadence.Current));

    public RainInterest? Nowcast(TimeSpan interval)
        => Style == RainGraphStyle.Nowcast && WebInput.AreCoordinates(Latitude, Longitude) ? new RainInterest(Latitude!.Value, Longitude!.Value, interval) : null;

    /// <summary>The hourly want, only in the hours look.</summary>
    public HoursInterest? Hourly() => Style == RainGraphStyle.Hours ? Interest() : null;

    public override WidgetConfig Clone()
    {
        var copy = new RainGraphConfig { Style = Style, ShowChance = ShowChance };
        CopyTo(copy);
        return copy;
    }
}
