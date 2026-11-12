using System.Text.Json.Serialization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Widget.UpNext;

namespace Themia.Core.Model;

/// <summary>
/// [S-11408] After this: which accounts and calendars it reads, as every calendar widget chooses them
/// ([S-562], [S-567]), and how many meetings it shows. Birthdays are all-day and never a meeting, so it
/// never asks for them.
/// </summary>
public sealed class UpNextConfig : CalendarConfig, IKitRows
{
    /// <summary>[S-11408] A new list's rows.</summary>
    public const int DefaultRows = 3;

    public UpNextConfig()
    {
        ShowBirthdays = false;
    }

    /// <summary>[S-1862] What its Set up row says it waits for.</summary>
    [JsonIgnore]
    public override string? SetupSentence => UpNextStr.UpNext_Setup_Sentence.Text();

    /// <summary>What is coming up: the agenda's shape.</summary>
    [JsonIgnore]
    public override CalendarStyle Layout => CalendarStyle.Agenda;

    /// <summary>[S-11408] How many meetings show; more scroll inside them. Its height follows.</summary>
    public int Rows { get; set; } = DefaultRows;

    /// <summary>[S-11408] One line a meeting: its title and its start. Not a setting.</summary>
    [JsonIgnore]
    bool IKitRows.OffersLines => false;

    [JsonIgnore]
    public int Lines
    {
        get => 1;
        set { }
    }

    /// <summary>[S-11408] The rows as drawn: the chosen count, one line each.</summary>
    [JsonIgnore]
    public KitRowsChoice RowsShown => new(KitSizing.ClampRows(Rows), 1);

    protected override void RepairOwn(List<string> repairs)
    {
        if (ShowBirthdays)
        {
            repairs.Add("after this never shows birthdays");
            ShowBirthdays = false;
        }

        if (Rows != KitSizing.ClampRows(Rows))
        {
            Rows = KitSizing.ClampRows(Rows);
            repairs.Add("after this had rows out of range");
        }
    }

    public override WidgetConfig Clone()
    {
        var copy = (UpNextConfig)CopySharedTo(new UpNextConfig());
        copy.Rows = Rows;
        return copy;
    }
}
