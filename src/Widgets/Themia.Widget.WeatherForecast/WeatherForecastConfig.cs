using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Items;
using Themia.Sdk.Kit;
using Themia.Widget.WeatherForecast;

namespace Themia.Core.Model;

/// <summary>
/// [S-6163] [S-16974] The forecast's styles: the item base's arrangements and the temperature curve (owner, 2026-11-23: "the current
/// weather forecast row view will be the new table view for this widget"; "weather should not loose any features"). Table is
/// what Rows was, Row list what Columns was, Grid the grid; List and Column list the base's; Curve the forecast's own chart.
/// </summary>
[JsonConverter(typeof(StyleJsonConverter<ForecastStyle>))]
public enum ForecastStyle
{
    /// <summary>[S-725] A row a day, its parts in aligned columns: what Rows was, the default.</summary>
    Table = 0,

    /// <summary>[S-726] The days side by side: what Columns was.</summary>
    RowList = 1,

    /// <summary>[S-6164] The days' highs and lows as two smooth curves across the week.</summary>
    Curve = 2,

    /// <summary>[S-6171] The days as cells in a grid.</summary>
    Grid = 3,

    /// <summary>[S-16941] Each day its own block, as a device list's.</summary>
    List = 4,

    /// <summary>[S-16943] The days in compact rows.</summary>
    ColumnList = 5,
}

/// <summary>
/// The next few days, from open-meteo's <c>daily</c> block.
/// <para>
/// A separate widget from the current conditions (<c>weatherNow</c>) rather than a mode of it, because the two
/// answer different questions and want different shapes on the desktop: one is a readout, the other
/// is a list.
/// </para>
/// </summary>
public sealed class WeatherForecastConfig : WeatherConfig, IStyled, IInterests, IItemParts
{
    [JsonIgnore]
    public override bool ReadsWeather => true;

    /// <summary>[S-11145] [S-14502] It asks for every day open-meteo gives: how many it shows is its card's.</summary>
    [JsonIgnore]
    public override int? ForecastDays => MostDays;

    /// <summary>[S-2608] What the background sync keeps fresh for it.</summary>
    public void AddInterests(InterestsBuilder interests)
    {
        if (WeatherInterests.For(this, interests.Unit, UpdateCadence.Interval(interests.Refresh, HostCadences.Weather)) is { } wants)
        {
            interests.Of<WeatherInterest>().Add(wants);
        }
    }

    private static readonly StyleOption[] ForecastStyles =
    [
        StyleOption.Of(ForecastStyle.Table, Str.Items_Style_Table.Local(), 0xE80A),
        StyleOption.Of(ForecastStyle.RowList, Str.Items_Style_RowList.Local(), 0xE907),
        StyleOption.Of(ForecastStyle.Curve, WeatherForecastStr.Styles_Forecast_Curve.Local(), 0xE9D2),
        StyleOption.Of(ForecastStyle.Grid, Str.Items_Style_Grid.Local(), 0xF0E2),
        StyleOption.Of(ForecastStyle.List, Str.Items_Style_List.Local(), 0xEA37),
        StyleOption.Of(ForecastStyle.ColumnList, Str.Items_Style_ColumnList.Local(), 0xE8FD),
    ];

    /// <summary>
    /// [S-11145] [S-14410] [S-14502] How many days a new card shows: its natural size's. The days shown after are as many as
    /// its card holds (1 to <see cref="MostDays"/>, open-meteo's own limit), never stored.
    /// </summary>
    public const int DefaultDays = 5;

    /// <summary>[S-11145] The most days it shows and asks for: open-meteo's own limit.</summary>
    public const int MostDays = 14;

    /// <summary>[S-16946] The parts left out: the sky in words (what was the table's second line) by default.</summary>
    public List<ItemPart> HiddenParts { get; set; } = [ItemPart.Subtitle];

    /// <summary>[S-16950] The parts' order: the table's columns.</summary>
    public List<ItemPart> PartOrder { get; set; } = [];

    /// <summary>[S-16673] The grid blocks between two days (not in the table).</summary>
    public int Gap { get; set; }

    /// <summary>[S-16952] The table's column titles (off: the days need none).</summary>
    public bool ColumnTitles { get; set; }

    /// <summary>Show each day's precipitation total beside its temperatures.</summary>
    public bool ShowPrecipitation { get; set; } = true;

    /// <summary>[S-6163] [S-16974] The table, the row list, the curve, the grid, the list or the column list.</summary>
    public ForecastStyle Style { get; set; } = ForecastStyle.Table;

    IReadOnlyList<StyleOption> IStyled.Styles => ForecastStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (ForecastStyle)value;
    }

    /// <summary>[S-16951] The item base's keys held to their range, and says so.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();
        Gap = ItemOrder.Repaired(Gap, "forecast", repairs);
        HiddenParts = ItemOrder.Repaired(HiddenParts, "forecast", repairs);
        PartOrder = ItemOrder.Repaired(PartOrder, "forecast", repairs);
        return repairs;
    }

    public override WidgetConfig Clone()
    {
        var copy = new WeatherForecastConfig
        {
            ShowPrecipitation = ShowPrecipitation,
            Style = Style,
            HiddenParts = [.. HiddenParts],
            PartOrder = [.. PartOrder],
            Gap = Gap,
            ColumnTitles = ColumnTitles,
        };

        CopyTo(copy);
        return copy;
    }
}
