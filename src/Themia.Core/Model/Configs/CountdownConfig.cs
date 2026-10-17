using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-6030] How the Countdown draws its duration: in words, a ring of the time left, or
/// split-flap cards.
/// </summary>
[JsonConverter(typeof(StyleJsonConverter<CountdownStyle>))]
public enum CountdownStyle
{
    Text = 0,

    /// <summary>[S-6031] The time left as a ring, from when the target was set to the target.</summary>
    Ring = 1,

    /// <summary>[S-6032] Days, hours and minutes on the Clock's split-flap cards.</summary>
    Flip = 2,
}

/// <summary>
/// [S-6036] One countdown: a target, its name and how it reads. A widget counts to one target;
/// several countdowns are several widgets. A new widget has no target and asks to be set up.
/// </summary>
public sealed class CountdownConfig : WidgetConfig, IStyled
{
    /// <summary>The most a name says, in characters.</summary>
    public const int MostLabel = 60;

    private static readonly StyleOption[] CountdownStyles =
    [
        StyleOption.Of(CountdownStyle.Text, Str.Styles_Countdown_Text, 0xE8D2),
        StyleOption.Of(CountdownStyle.Ring, Str.Styles_Countdown_Ring, 0xEA3A),
        StyleOption.Of(CountdownStyle.Flip, Str.Styles_Clock_Flip, 0xE8A9),
    ];

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Countdown;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("countdown"),
        Kind = WidgetKind.Countdown,
    };

    /// <summary>[S-6030] The duration's look.</summary>
    public CountdownStyle Style { get; set; } = CountdownStyle.Text;

    IReadOnlyList<StyleOption> IStyled.Styles => CountdownStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (CountdownStyle)value;
    }

    /// <summary>Its name ("Holiday"); empty reads "Untitled".</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>[S-6036] The instant counted to, stored in UTC; null until it is set ([S-1860]). [C-53]</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? Target { get; set; }

    public CountdownFormat Format { get; set; } = CountdownFormat.Auto;

    /// <summary>
    /// [S-1448] When the target was set, so the tile can show how far along it is. The default
    /// (unset) means "unknown": no progress is drawn rather than one measured from year one.
    /// </summary>
    public DateTimeOffset Created { get; set; }

    /// <summary>
    /// [S-142] [C-20] One second only when the format shows seconds; otherwise half a minute, which is
    /// enough for minute granularity and costs the process 120 fewer wakes a minute.
    /// </summary>
    [JsonIgnore]
    public TimeSpan TickInterval => Format == CountdownFormat.DaysHoursMinutesSeconds
        ? TimeSpan.FromSeconds(1)
        : TimeSpan.FromSeconds(30);

    /// <summary>[S-6036] Point the countdown at <paramref name="target"/>, set <paramref name="now"/>: its progress starts again.</summary>
    public void Retarget(DateTimeOffset target, DateTimeOffset now)
    {
        Target = target.ToUniversalTime();
        Created = now.ToUniversalTime();
    }

    public override WidgetConfig Clone() => new CountdownConfig
    {
        Style = Style,
        Label = Label,
        Target = Target,
        Format = Format,
        Created = Created,
    };

    /// <summary>[S-6036] A hand-edited file: an unknown format reads Auto, a name is one line of at most <see cref="MostLabel"/>.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();

        if (!Enum.IsDefined(Format))
        {
            repairs.Add($"countdown: format {(int)Format} is not one; Auto");
            Format = CountdownFormat.Auto;
        }

        var name = (Label ?? string.Empty).ReplaceLineEndings(" ").Trim();
        name = name.Length <= MostLabel ? name : name[..MostLabel].TrimEnd();
        if (name != Label)
        {
            repairs.Add("countdown: the name was not one short line");
            Label = name;
        }

        return repairs;
    }
}
