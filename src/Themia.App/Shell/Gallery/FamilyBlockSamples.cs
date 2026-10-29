#if DEBUG
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-6732] Debug only: the blocks' variants over fixed sample data, the same the mockups draw
/// (TestResults/family/mockups/blocks.py, <c>VARIANTS</c>): each a function of the card's size.
/// The layout around the blocks is the family's: the header in the header inset, the content from
/// 44 (16 without a header), rows full width with their own inset, everything else inside the inset.
/// </summary>
internal static class FamilyBlockSamples
{
    private static string G(int code) => char.ConvertFromUtf32(code);

    private static readonly string[] Tasks =
    [
        "Book the dentist appointment", "Reply to the landlord about the heating", "Pick up the parcel", "Renew the passport before the trip in spring",
        "Water the plants", "Send the invoice", "Call Grandma", "Back up the laptop", "Order printer ink", "Plan the weekend", "Fix the bike light", "Return the library books",
    ];

    private static readonly (int Glyph, string Name, string Value)[] Facts =
    [
        (0xE9CA, "Feels like", "13°C"), (0xEB42, "Humidity", "71%"), (0xE7BC, "Wind", "13 km/h SW"), (0xEB42, "Precipitation", "10%"),
        (0xEC4A, "Pressure", "1016 hPa"), (0xE706, "UV index", "2"), (0xE753, "Cloud cover", "58%"), (0xE7BC, "Gusts", "27 km/h"),
        (0xE706, "Sunrise", "7:41 AM"), (0xE708, "Sunset", "6:32 PM"), (0xE753, "Visibility", "24 km"), (0xE9CA, "Dew point", "8°C"),
    ];

    private static readonly (string Day, int Glyph, int Lo, int Hi)[] Days =
    [
        ("Today", 0xE753, 8, 16), ("Fri", 0xEA91, 9, 13), ("Sat", 0xE753, 7, 12), ("Sun", 0xE706, 5, 15), ("Mon", 0xE706, 6, 17), ("Tue", 0xEA91, 9, 15),
        ("Wed", 0xE7BC, 6, 12), ("Thu", 0xE753, 7, 14), ("Fri", 0xE706, 5, 16), ("Sat", 0xEA91, 8, 13), ("Sun", 0xE753, 6, 15),
    ];

    private static readonly (int Glyph, string Name)[] Cells =
    [
        (0xE74D, "Recycle Bin"), (0xE7F4, "This PC"), (0xE896, "Downloads"), (0xE8A5, "Documents"), (0xE8FC, "Desktop"), (0xEB9F, "Pictures"),
        (0xE8D6, "Music"), (0xE714, "Videos"), (0xE8B7, "Projects"), (0xE77B, "Home"), (0xE8F1, "News"), (0xE787, "Calendar"),
    ];

    private static readonly double[] Cpu = [0.42, 0.46, 0.44, 0.47, 0.40, 0.33, 0.34, 0.32, 0.36, 0.58, 0.66, 0.50, 0.36, 0.30, 0.28, 0.42, 0.60, 0.55, 0.40];
    private static readonly double[] Kernel = [0.12, 0.16, 0.11, 0.15, 0.10, 0.14, 0.11, 0.09, 0.13, 0.15, 0.12, 0.10, 0.14, 0.11, 0.12, 0.10, 0.13, 0.15, 0.16];

    /// <summary>Every variant, by name, in the order the mockups draw them.</summary>
    public static IReadOnlyList<(string Name, Func<double, double, FrameworkElement> Build)> Variants { get; } =
    [
        ("header-plain", (w, h) => Card(w, h, Header(0xE950, "CPU"))),
        ("header-trail", (w, h) => Card(w, h, Header(0xE9D5, "Errands", HeaderKind.Trail))),
        ("header-state", (w, h) => Card(w, h, Header(0xE8F1, "World News, Field notes", HeaderKind.State))),
        ("row-list", ListRows),
        ("row-fact", FactRows),
        ("row-meter", MeterRows),
        ("row-cell", CellGrid),
        ("readout", (w, h) => ReadoutCard(w, h, 0xE950, "CPU", new ReadoutText("62", "%", LineUnder: true), r => r.Show("62", "%", "Utilisation"))),
        ("readout-lead", (w, h) => ReadoutCard(w, h, 0xE753, "Zürich", new ReadoutText("14", "°C", Lead: true, LineUnder: true), r => r.Show("14", "°C", "Partly cloudy", lead: G(0xE9CA)))),
        ("readout-delta", (w, h) => ReadoutCard(w, h, 0xE9D2, "Apple", new ReadoutText("188.42", " USD", LineUnder: true), r => r.Show("188.42", " USD", "Apple Inc.", "−1.2 %", ColourRole.Critical))),
        ("readout-hero", HeroReadout),
        ("chart-band", (w, h) => BandCard(w, h, "62", ColourRole.Accent)),
        ("chart-caution", (w, h) => BandCard(w, h, "91", ColourRole.Caution)),
        ("chart-bar", BarCard),
        ("chart-ring", RingCard),
        ("status-empty", (w, h) => StatusCard(w, h, 0xE8F1, "Feed", StatusKind.Empty, "This feed has no articles", null)),
        ("status-problem", (w, h) => StatusCard(w, h, 0xE9D5, "Errands", StatusKind.Problem, "No network", "Try again")),
        ("status-setup", (w, h) => StatusCard(w, h, 0xE9D5, "Tasks", StatusKind.Setup, "Choose a list to show", "Set up")),
        ("tile-off", (w, h) => TileCard(w, h, TileLook.Tile, on: false)),
        ("tile-on", (w, h) => TileCard(w, h, TileLook.Tile, on: true)),
        ("tile-round-on", (w, h) => TileCard(w, h, TileLook.Round, on: true)),
        ("tile-round-off", (w, h) => TileCard(w, h, TileLook.Round, on: false)),
        ("tile-switch", (w, h) => TileCard(w, h, TileLook.Switch, on: true)),
        ("tile-unavailable", (w, h) => TileCard(w, h, TileLook.Tile, on: false, available: false)),
        ("controls", ControlsCard),
        ("controls-slider", SliderCard),
    ];

    private enum HeaderKind
    {
        Plain,
        Trail,
        State,
    }

    private static Grid Card(double w, double h, FrameworkElement? header, FrameworkElement? content = null, bool inset = true)
    {
        var grid = new Grid { Width = w, Height = h };
        if (header is not null)
        {
            grid.Children.Add(header);
        }

        if (content is not null)
        {
            var top = header is null ? FamilySpace.Inset : FamilySpace.ContentTop;
            var side = inset ? FamilySpace.Inset : 0;
            content.Margin = new Thickness(side, top, side, FamilySpace.Inset);
            grid.Children.Add(content);
        }

        return grid;
    }

    private static Grid Header(int glyph, string name, HeaderKind kind = HeaderKind.Plain)
    {
        HeaderParts? parts = null;
        if (kind == HeaderKind.Trail)
        {
            parts = new HeaderParts();
            parts.Add(HeaderPartKind.Count, HeaderBlock.Count(new TextBlock { Text = "10" }));
            parts.Add(HeaderPartKind.Button, HeaderBlock.Button(G(0xE710), "Add a task", () => { }));
            parts.Add(HeaderPartKind.Button, HeaderBlock.Button(G(0xE712), "More", () => { }));
        }
        else if (kind == HeaderKind.State)
        {
            parts = new HeaderParts();
            parts.Add(HeaderPartKind.Problem, HeaderBlock.Problem("Field notes: No network"));
            parts.Add(HeaderPartKind.Busy, HeaderBlock.Busy());
            parts.Add(HeaderPartKind.Label, HeaderBlock.Label("Just now"));
        }

        var block = new HeaderBlock(G(glyph), new TextBlock { Text = name }, parts);
        return new Grid { Padding = Family.HeaderInset, VerticalAlignment = VerticalAlignment.Top, Children = { block } };
    }

    /// <summary>The view area a card gives its content under a header, for the rules that classify it.</summary>
    private static WidgetArea Area(double w, double h, bool header = true) => new(w, h - (header ? FamilySpace.ContentTop : 0));

    private static double ContentHeight(double h, bool header = true) => h - (header ? FamilySpace.ContentTop : FamilySpace.Inset) - FamilySpace.Inset;

    private static FrameworkElement ListRows(double w, double h)
    {
        var lines = RowRules.Lines(Area(w, h), 3);
        var stack = new StackPanel();
        var n = RowRules.Fit(ContentHeight(h), lines);
        for (var i = 0; i < Math.Min(n, Tasks.Length); i++)
        {
            var row = new RowBlock(RowKind.List);
            row.LeadGlyph(G(0xEA3A));
            row.Show(Tasks[i], i % 3 == 0 ? "Today" : null, "Errands · due Friday", "Repeats every week");
            row.Lines = lines;
            stack.Children.Add(row);
        }

        return Card(w, h, Header(0xE9D5, "Errands"), stack, inset: false);
    }

    private static FrameworkElement FactRows(double w, double h)
    {
        var stack = new StackPanel();
        var n = RowRules.Fit(ContentHeight(h), 1);
        for (var i = 0; i < Math.Min(n, Facts.Length); i++)
        {
            var row = new RowBlock(RowKind.Fact);
            row.LeadGlyph(G(Facts[i].Glyph));
            row.Show(Facts[i].Name, Facts[i].Value);
            stack.Children.Add(row);
        }

        return Card(w, h, Header(0xE753, "Zürich"), stack, inset: false);
    }

    private static FrameworkElement MeterRows(double w, double h)
    {
        var stack = new StackPanel();
        var n = RowRules.Fit(ContentHeight(h), 1);
        var cw = w - (2 * FamilySpace.Inset);
        var meter = cw >= 240 ? Math.Min(140, Math.Floor(cw * 0.32)) : 0;
        for (var i = 0; i < Math.Min(n, Days.Length); i++)
        {
            var (day, glyph, lo, hi) = Days[i];
            var row = new RowBlock(RowKind.List) { Strong = i == 0 };
            row.LeadGlyph(G(glyph));
            row.Show(day, $"{lo}° {hi}°");
            if (meter > 0)
            {
                var bar = new ChartBar();
                bar.Show((hi - 5) / 12.0, (lo - 5) / 12.0);
                row.Meter(bar, meter);
            }

            stack.Children.Add(row);
        }

        return Card(w, h, Header(0xE9CA, "Zürich"), stack, inset: false);
    }

    private static FrameworkElement CellGrid(double w, double h)
    {
        var area = Area(w, h);
        var cw = w - (2 * FamilySpace.Inset);
        var ch = ContentHeight(h);
        var icon = RowRules.CellIcon(area);
        var named = cw >= 240;
        var cellWidth = named ? icon + (2 * FamilySpace.S6) : icon;
        var cellHeight = RowRules.CellHeight(icon, named);
        var cols = Math.Max(1, (int)Math.Floor((cw + FamilySpace.S3) / (cellWidth + FamilySpace.S3)));
        var rows = Math.Max(0, (int)Math.Floor((ch + FamilySpace.S3) / (cellHeight + FamilySpace.S3)));
        var canvas = new Canvas();
        for (var i = 0; i < Math.Min(cols * rows, Cells.Length); i++)
        {
            var cell = new RowCell(icon) { Width = cellWidth };
            cell.Show(G(Cells[i].Glyph), named ? Cells[i].Name : string.Empty);
            Canvas.SetLeft(cell, i % cols * (cellWidth + FamilySpace.S3));
            Canvas.SetTop(cell, i / cols * (cellHeight + FamilySpace.S3));
            canvas.Children.Add(cell);
        }

        return Card(w, h, Header(0xE71B, "Shortcuts"), canvas);
    }

    private static FrameworkElement ReadoutCard(double w, double h, int glyph, string name, ReadoutText worst, Action<ReadoutBlock> show)
    {
        var readout = new ReadoutBlock { VerticalAlignment = VerticalAlignment.Top };
        show(readout);
        readout.Size(Area(w, h), w - (2 * FamilySpace.Inset), ContentHeight(h), worst);
        return Card(w, h, Header(glyph, name), readout);
    }

    private static FrameworkElement HeroReadout(double w, double h)
    {
        var readout = new ReadoutBlock { VerticalAlignment = VerticalAlignment.Center };
        readout.Show("10:42", " AM", "Thursday, October 29");
        readout.Size(Area(w, h, header: false), w - (2 * FamilySpace.Inset), ContentHeight(h, header: false), new ReadoutText("10:42", " AM", LineUnder: true, OneReadout: true));
        return Card(w, h, null, readout);
    }

    private static FrameworkElement BandCard(double w, double h, string reading, ColourRole role)
    {
        var readout = new ReadoutBlock { VerticalAlignment = VerticalAlignment.Top };
        readout.Show(reading, "%");
        readout.Size(Area(w, h), w - (2 * FamilySpace.Inset), ContentHeight(h), new ReadoutText("100", "%"));
        var card = Card(w, h, Header(0xE950, "CPU"), readout);

        var bandTop = FamilySpace.ContentTop + readout.Step.LineHeight + FamilySpace.S2;
        var band = new ChartSeries { Margin = new Thickness(0, bandTop, 0, 0) };
        band.Role(role);
        band.Show(Cpu, Kernel);
        card.Children.Add(band);
        return card;
    }

    private static FrameworkElement BarCard(double w, double h)
    {
        var readout = new ReadoutBlock();
        readout.Show("64", "%");
        readout.Size(Area(w, h), w - (2 * FamilySpace.Inset), ContentHeight(h) - FamilySpace.S2 - FamilyShape.Track, new ReadoutText("100", "%"));
        var bar = new ChartBar { Margin = new Thickness(0, FamilySpace.S2, 0, 0) };
        bar.Show(0.64);
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Top, Children = { readout, bar } };
        return Card(w, h, Header(0xE83F, "Battery"), stack);
    }

    private static FrameworkElement RingCard(double w, double h)
    {
        var size = Math.Min(w - (2 * FamilySpace.Inset), ContentHeight(h));
        var ring = new ChartRing { Width = size, Height = size, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        ring.Show(0.72);
        return Card(w, h, Header(0xEDA2, "Disk"), ring);
    }

    private static FrameworkElement StatusCard(double w, double h, int glyph, string name, StatusKind kind, string sentence, string? action)
    {
        var status = new StatusBlock();
        status.Show(kind, G(glyph), sentence, action, action is null ? null : () => { });
        return Card(w, h, Header(glyph, name), status, inset: false);
    }

    private static FrameworkElement TileCard(double w, double h, TileLook look, bool on, bool available = true)
    {
        var tile = new TileBlock(look) { Padding = Family.Inset };
        tile.Show(G(0xE701), "Wi-Fi", available ? (on ? "On" : "Off") : "Not on this PC", on, available);
        tile.Size(w - (2 * FamilySpace.Inset), h - (2 * FamilySpace.Inset));
        return new Grid { Width = w, Height = h, Children = { tile } };
    }

    private static FrameworkElement ControlsCard(double w, double h)
    {
        var controls = new ControlsBlock { VerticalAlignment = VerticalAlignment.Bottom };
        controls.Main("Start", () => { });
        controls.Icon(G(0xE72C), "Reset", () => { });
        controls.Icon(G(0xE893), "Skip", () => { });

        var readout = new ReadoutBlock { VerticalAlignment = VerticalAlignment.Top };
        readout.Show("25:00", string.Empty, "Focus");
        readout.Size(Area(w, h), w - (2 * FamilySpace.Inset), ContentHeight(h) - FamilyShape.Button - FamilySpace.S2, new ReadoutText("25:00", LineUnder: true));
        var content = new Grid { Children = { readout, controls } };
        return Card(w, h, Header(0xE916, "Timer"), content);
    }

    private static FrameworkElement SliderCard(double w, double h)
    {
        var controls = new ControlsBlock { VerticalAlignment = VerticalAlignment.Bottom };
        controls.Icon(G(0xE767), "Mute", () => { });
        controls.Slider(0, 100, 40, _ => { });

        var readout = new ReadoutBlock { VerticalAlignment = VerticalAlignment.Top };
        readout.Show("40", "%");
        readout.Size(Area(w, h), w - (2 * FamilySpace.Inset), ContentHeight(h) - FamilyShape.Button - FamilySpace.S2, new ReadoutText("100", "%"));
        var content = new Grid { Children = { readout, controls } };
        return Card(w, h, Header(0xE767, "Volume"), content);
    }
}
#endif
