using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Shell.Config;

/// <summary>The Clock's settings rows. Each writes straight to the live config and saves.</summary>
internal static partial class WidgetSettings
{
    /// <summary>How many zones the search drops down at once: a list longer than the screen is a wall.</summary>
    private const int ZoneSuggestions = 40;

    public static void Clock(List<FrameworkElement> rows, ClockConfig clock, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(Str.Widget_Clock_Label.Text()));

        // [S-6010] The style is the shared Style row the page builder puts first.
        // [S-5303] The date style has no time, date line, label or offset: only where it sits and
        // whose day it is (the zone) apply, so only those rows are offered for it.
        var date = clock.Style == ClockStyle.Date;

        if (!date)
        {
            rows.Add(FluentLayout.Row(Glyphs.Timer, Str.Clock_SecondsLabel.Text(),
                Str.WidgetsA_Clock_Settings_Seconds_Desc.Text(),
                FluentLayout.Toggle(clock.ShowSeconds, value => { clock.ShowSeconds = value; save(); })));

            rows.Add(FluentLayout.Row(Glyphs.Calendar, Str.Clock_DateLabel.Text(), null,
                FluentLayout.Toggle(clock.ShowDate, value => { clock.ShowDate = value; save(); })));
        }

        rows.Add(FluentLayout.Row(Glyphs.View, Str.Clock_AlignLabel.Text(), null,
            FluentLayout.Choice(
                [(ClockAlign.Left, Str.Clock_AlignLeft.Text()), (ClockAlign.Center, Str.Clock_AlignCenter.Text()), (ClockAlign.Right, Str.Clock_AlignRight.Text())],
                clock.Align,
                value => { clock.Align = value; save(); })));

        if (date)
        {
            rows.Add(FluentLayout.Row(Glyphs.Place, Str.Clock_TimezoneLabel.Text(), Str.WidgetsA_Clock_Settings_Zone_Desc.Text(),
                ZoneSearch(clock, save, () => { })));
            return;
        }

        // [S-1476] The place and the date larger (user request: both were hard to read) or smaller.
        rows.Add(FluentLayout.Row(Glyphs.Font, Str.ClockText_SizeLabel.Text(), Str.ClockText_SizeDesc.Text(),
            FluentLayout.Choice(
                [(ClockTextSize.Small, Str.ClockText_Small.Text()), (ClockTextSize.Default, Str.Common_Default.Text()), (ClockTextSize.Large, Str.ClockText_Large.Text())],
                clock.TextSize,
                value => { clock.TextSize = value; save(); })));

        // The label box is built first so the zone search can update its placeholder: an empty label
        // shows the chosen city, and the placeholder says which.
        var label = LabelBox(clock, save);

        // [S-3795] The label and the offset line fold under the zone they describe.
        rows.Add(FluentLayout.Expander(Glyphs.Place, Str.Clock_TimezoneLabel.Text(), Str.WidgetsA_Clock_Settings_Zone_Desc.Text(),
            ZoneSearch(clock, save, () => label.PlaceholderText = LabelPlaceholder(clock)),
            [
                FluentLayout.Row(Glyphs.Rename, Str.Clock_LabelLabel.Text(), Str.WidgetsA_Clock_Settings_Label_Desc.Text(), label),
                FluentLayout.Row(Glyphs.Hours, Str.Clock_OffsetLabel.Text(), Str.WidgetsA_Clock_Settings_Offset_Desc.Text(),
                    FluentLayout.Choice(
                        [
                            (ClockOffsetMode.None, Str.Common_None.Text()),
                            (ClockOffsetMode.Utc, Str.WidgetsA_Clock_Settings_Offset_Utc.Text()),
                            (ClockOffsetMode.Local, Str.WidgetsA_Clock_Settings_Offset_Local.Text()),
                        ],
                        clock.OffsetMode,
                        value => { clock.OffsetMode = value; save(); })),
            ]));
    }

    /// <summary>
    /// [S-1481] The zone picker: a search over every IANA city Windows maps, the machine's own zone
    /// always first as "Current: …" (an unset zone). Picking writes the IANA id; it resolves through
    /// <see cref="ZoneCatalog"/> because IANA ids do not resolve under <c>InvariantGlobalization</c>.
    /// </summary>
    private static AutoSuggestBox ZoneSearch(ClockConfig clock, Action save, Action picked)
    {
        // Windows writes "(UTC+01:00) Amsterdam, Berlin, …"; the offset moves with daylight saving and
        // is shown on every other line anyway, so the local choice names the places alone.
        var local = TimeZoneInfo.Local.DisplayName;
        var cut = local.IndexOf(") ", StringComparison.Ordinal);
        var places = local.StartsWith("(UTC", StringComparison.Ordinal) && cut > 0 ? local[(cut + 2)..] : local;
        var current = Str.Clock_TzCurrent.Format(("city", places));

        var box = new AutoSuggestBox
        {
            Width = 300,
            PlaceholderText = Str.LocationSearch_Placeholder.Text(),
            Text = ZoneText(clock.TimeZoneId) ?? current,
            QueryIcon = new SymbolIcon(Symbol.Find),
        };

        var hits = new List<ZoneEntry?>();

        void Suggest(string query)
        {
            // The local choice heads the list whatever is typed, so getting back is one click.
            hits = [null, .. ZoneCatalog.Search(query, ZoneSuggestions).Select(entry => (ZoneEntry?)entry)];
            box.ItemsSource = hits.Select(hit => hit is { } entry ? DescribeZone(entry) : current).ToList();
        }

        box.GotFocus += (_, _) =>
        {
            if (box.ItemsSource is null)
            {
                Suggest(string.Empty);
            }

            box.IsSuggestionListOpen = true;
        };

        box.TextChanged += (_, args) =>
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                Suggest(box.Text);
            }
        };

        void Pick(ZoneEntry? hit)
        {
            clock.TimeZoneId = hit?.IanaId;
            box.Text = ZoneText(clock.TimeZoneId) ?? current;
            save();
            picked();
        }

        box.SuggestionChosen += (_, args) =>
        {
            var index = box.ItemsSource is List<string> items ? items.IndexOf(args.SelectedItem as string ?? string.Empty) : -1;

            if (index >= 0 && index < hits.Count)
            {
                Pick(hits[index]);
            }
        };

        // Enter without choosing takes the best match, the way Windows' own search boxes do.
        box.QuerySubmitted += (_, args) =>
        {
            if (args.ChosenSuggestion is null && hits.Count > 1)
            {
                Pick(hits[1]);
            }
        };

        return box;
    }

    /// <summary>[S-1482] The label box. Written on Enter or leaving it, never per keystroke.</summary>
    private static TextBox LabelBox(ClockConfig clock, Action save)
    {
        var box = new TextBox
        {
            MinWidth = 200,
            MaxLength = ClockConfig.MaxLabelLength,
            Text = clock.CustomLabel ?? string.Empty,
            PlaceholderText = LabelPlaceholder(clock),
        };

        void Commit()
        {
            var text = box.Text.Trim();
            var value = text.Length == 0 ? null : text;

            if (value == clock.CustomLabel)
            {
                return;
            }

            clock.CustomLabel = value;
            save();
        }

        box.LostFocus += (_, _) => Commit();
        box.KeyDown += (_, args) =>
        {
            if (args.Key == Windows.System.VirtualKey.Enter)
            {
                Commit();
                args.Handled = true;
            }
        };

        return box;
    }

    private static string LabelPlaceholder(ClockConfig clock)
        => ZoneCatalog.Find(clock.TimeZoneId) is { } entry ? entry.City : Str.WidgetsA_Clock_Settings_Label_None.Text();

    /// <summary>"Tokyo · Asia" for a chosen zone; null for the local one.</summary>
    private static string? ZoneText(string? ianaId)
        => ZoneCatalog.Find(ianaId) is { } entry ? DescribeZone(entry, offset: false) : null;

    /// <summary>"Tokyo · Asia · UTC+9", the offset as of now (it moves with daylight saving).</summary>
    private static string DescribeZone(ZoneEntry entry) => DescribeZone(entry, offset: true);

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
