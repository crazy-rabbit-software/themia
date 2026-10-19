using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.Clock;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-1480] The Clock's options, all on its properties menu ([S-3719]): under Style what the look
/// draws (seconds, the date line, the text size: [S-6074] only the ones the style draws); then
/// Appearance ([S-3723]) and the time zone, a search ([S-3722]) with the offset line under it. It has no settings page.
/// </summary>
internal static class ClockMenu
{
    /// <summary>How many zones the search shows at once: a list longer than the screen is a wall.</summary>
    private const int ZoneSuggestions = 40;

    public static void Lines(WidgetMenuLines menu, ClockConfig clock)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(clock);

        var look = menu.StyleLines;
        if (clock.Offers(ClockSetting.Seconds))
        {
            look.Add(menu.Toggle(null, ClockStr.Clock_SecondsLabel.Text(), clock.ShowSeconds, on => clock.ShowSeconds = on));
        }

        if (clock.Offers(ClockSetting.Date))
        {
            look.Add(menu.Toggle(null, ClockStr.Clock_DateLabel.Text(), clock.ShowDate, on => clock.ShowDate = on));
        }

        // [S-1476] The place and the date larger (user request: both were hard to read) or smaller.
        if (clock.Offers(ClockSetting.TextSize))
        {
            look.Add(menu.Choice(Glyphs.Font, ClockStr.ClockText_SizeLabel.Text(),
                [(ClockTextSize.Small, ClockStr.ClockText_Small.Text()), (ClockTextSize.Default, Str.Common_Default.Text()), (ClockTextSize.Large, ClockStr.ClockText_Large.Text())],
                clock.TextSize, value => clock.TextSize = value));
        }

        // [S-1478] The look (Style, Appearance), a divider, then what time it tells.
        menu.PlaceCard();
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.Add(Zone(menu, clock));
    }

    /// <summary>
    /// [S-1481] The zone: a search over every IANA city Windows maps, the machine's own zone always
    /// first as "Current: …" (an unset zone), the chosen one with the dot. Picking writes the IANA id;
    /// it resolves through <see cref="ZoneCatalog"/> because IANA ids do not resolve under
    /// <c>InvariantGlobalization</c>. [S-3795] Under the answers: the offset line.
    /// </summary>
    private static ItemMenuEntry Zone(WidgetMenuLines menu, ClockConfig clock)
    {
        var current = Current();
        var group = ClockStr.Clock_TimezoneLabel.Text();

        Task<MenuSearchAnswer> Find(string query, CancellationToken cancel)
        {
            // The local choice heads the list whatever is typed, so getting back is one click.
            List<ItemMenuEntry> lines = [Pick(null, current)];
            lines.AddRange(ZoneCatalog.Search(query, ZoneSuggestions).Select(entry => Pick(entry.IanaId, DescribeZone(entry, offset: true))));
            return Task.FromResult(new MenuSearchAnswer(lines));
        }

        ItemMenuEntry Pick(string? ianaId, string text) => new(null, text, () =>
        {
            if (clock.TimeZoneId != ianaId)
            {
                clock.TimeZoneId = ianaId;
                menu.Save();
            }
        }, Checked: clock.TimeZoneId == ianaId, Group: group);

        var under = new List<ItemMenuEntry>();
        if (clock.Offers(ClockSetting.Offset))
        {
            under.Add(menu.Choice(Glyphs.Hours, ClockStr.Clock_OffsetLabel.Text(),
                [
                    (ClockOffsetMode.None, Str.Common_None.Text()),
                    (ClockOffsetMode.Utc, ClockStr.WidgetsA_Clock_Settings_Offset_Utc.Text()),
                    (ClockOffsetMode.Local, ClockStr.WidgetsA_Clock_Settings_Offset_Local.Text()),
                ],
                clock.OffsetMode, value => clock.OffsetMode = value));
        }

        return WidgetMenuLines.Search(Glyphs.Place, group, ZoneText(clock.TimeZoneId) ?? current,
            new MenuSearch(ClockStr.LocationSearch_Placeholder.Text(), Find, under));
    }

    /// <summary>"Current: Amsterdam, Berlin, …": the machine's own zone, its places without the offset (it moves with daylight saving).</summary>
    private static string Current()
    {
        var local = TimeZoneInfo.Local.DisplayName;
        var cut = local.IndexOf(") ", StringComparison.Ordinal);
        var places = local.StartsWith("(UTC", StringComparison.Ordinal) && cut > 0 ? local[(cut + 2)..] : local;
        return ClockStr.Clock_TzCurrent.Format(("city", places));
    }

    /// <summary>"Tokyo · Asia" for a chosen zone; null for the local one.</summary>
    private static string? ZoneText(string? ianaId)
        => ZoneCatalog.Find(ianaId) is { } entry ? DescribeZone(entry, offset: false) : null;

    /// <summary>"Tokyo · Asia · UTC+9", the offset as of now (it moves with daylight saving).</summary>
    private static string DescribeZone(ZoneEntry entry, bool offset)
    {
        var text = entry.Region.Length == 0 ? entry.City : $"{entry.City} · {entry.Region}";

        if (!offset || ZoneCatalog.Resolve(entry.IanaId) is not { } zone)
        {
            return text;
        }

        return $"{text} · {ClockFormatter.OffsetText(zone.GetUtcOffset(DateTimeOffset.UtcNow), TimeSpan.Zero, ClockOffsetMode.Utc, Strings.Current)}";
    }
}
