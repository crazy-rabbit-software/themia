using System.Text.Json;
using Themia.Core.Diagnostics;
using Themia.Sdk;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-16746] The gallery's layout files: one per tab, <c>src/Themia.Core/Widgets/GalleryLayouts/&lt;tab&gt;.json</c> in the
/// repo, built into Core (owner, 2026-11-23: "In the repo, shipped": every user sees the curated gallery, the owner's edits a
/// change to commit). Read from the build (<see cref="Load"/>), checked where read (<see cref="Checked"/>: a widget the build
/// does not have, a preset or style its kind does not have, dropped loudly; a size, a column's width or a widget's place out
/// of range, missing or on another widget repaired, loudly), written as the editor saves them (<see cref="Write"/>: stable,
/// two spaces, LF).
/// </summary>
public static class CuratedLayouts
{
    private const string Scope = "gallery-layouts";

    /// <summary>The files' folder, from the repo's root.</summary>
    public const string Folder = "src/Themia.Core/Widgets/GalleryLayouts";

    /// <summary>The most cells a widget is stored at, each way (a 4K display's width is about 380).</summary>
    public const int MostCells = 400;

    private const string Resource = "gallery-layouts/";

    /// <summary>A tab's file: its id in camelCase (<c>calendarMail.json</c>).</summary>
    public static string FileName(AddFolderId tab)
    {
        var name = tab.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..] + ".json";
    }

    /// <summary>The tabs the build has a layout file for.</summary>
    public static IReadOnlyList<AddFolderId> Shipped()
        => [.. Enum.GetValues<AddFolderId>().Where(t => typeof(CuratedLayouts).Assembly.GetManifestResourceInfo(Resource + FileName(t)) is not null)];

    /// <summary>
    /// [S-16746] <paramref name="tab"/>'s layout as the build ships it, checked (its widgets' titles <paramref name="title"/>
    /// rows tall, [S-17222]); null where it has none (the tab falls back to the automatic bento until curated), or where its
    /// file cannot be read (logged: the same fallback).
    /// </summary>
    public static CuratedLayout? Load(AddFolderId tab, int title)
    {
        using var stream = typeof(CuratedLayouts).Assembly.GetManifestResourceStream(Resource + FileName(tab));
        if (stream is null)
        {
            return null;
        }

        try
        {
            using var reader = new StreamReader(stream);
            return Checked(Read(reader.ReadToEnd()), FileName(tab), title);
        }
        catch (JsonException ex)
        {
            Log.Error(Scope, $"the gallery's layout {FileName(tab)} is not a layout; the tab is laid automatically", ex);
            return null;
        }
    }

    /// <summary>A layout file's text read (nothing checked yet); a text that is no layout throws <see cref="JsonException"/>.</summary>
    public static CuratedLayout Read(string json)
        => JsonSerializer.Deserialize(json, CuratedLayoutJson.Default.CuratedLayout) ?? throw new JsonException("a layout file holds null");

    /// <summary>[S-16746] A layout as its file holds it: two spaces, LF line ends, a line end last.</summary>
    public static string Write(CuratedLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return JsonSerializer.Serialize(layout,CuratedLayoutJson.Default.CuratedLayout).ReplaceLineEndings("\n") + "\n";
    }

    /// <summary>
    /// [S-16746] <paramref name="layout"/> checked (<paramref name="file"/> names it in the log): a widget whose
    /// <paramref name="problem"/> is not null (the build's registry by default, <see cref="Problem"/>) dropped, a size outside 1 to
    /// <see cref="MostCells"/> held to it, a column's width that is missing or not 1 to <see cref="MostCells"/> blocks made its
    /// widest widget's (<see cref="CuratedBento.NewColumn"/> where it has none) ([S-17162]); [S-17222] a widget's place that is
    /// missing or on another widget (its card or, with titles <paramref name="title"/> rows tall, its title;
    /// <see cref="CuratedBento.Clashes"/>) made the first free spot of its section (<see cref="CuratedBento.FirstFree"/>, sizes
    /// as stored), one past its column's right edge moved left into it; each logged. Never half-built.
    /// </summary>
    public static CuratedLayout Checked(CuratedLayout layout, string file, int title, Func<CuratedWidget, string?>? problem = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        problem ??= Problem;
        title = layout.Titles ? Math.Max(0, title) : 0;
        var columns = new List<CuratedColumn>();
        foreach (var column in layout.Columns ?? [])
        {
            var sections = new List<CuratedSection>();
            foreach (var section in column.Sections ?? [])
            {
                var widgets = new List<CuratedWidget>();
                foreach (var widget in section.Widgets ?? [])
                {
                    if (widget is null)
                    {
                        Log.Warn(Scope, $"{file}: an empty widget entry dropped");
                        continue;
                    }

                    if (problem(widget) is { } why)
                    {
                        Log.Warn(Scope, $"{file}: '{widget.Identity}' dropped: {why}");
                        continue;
                    }

                    var (w, h) = (Math.Clamp(widget.W, 1, MostCells), Math.Clamp(widget.H, 1, MostCells));
                    if (w != widget.W || h != widget.H)
                    {
                        Log.Warn(Scope, $"{file}: '{widget.Identity}' at {widget.W}x{widget.H} cells held to {w}x{h}");
                    }

                    widgets.Add(widget with { W = w, H = h });
                }

                sections.Add(section with { Title = string.IsNullOrWhiteSpace(section.Title) ? null : section.Title.Trim(), Widgets = widgets });
            }

            var width = column.Width;
            if (width < 1 || width > MostCells)
            {
                width = sections.SelectMany(s => s.Widgets).Select(w => w.W).DefaultIfEmpty(CuratedBento.NewColumn).Max();
                Log.Warn(Scope, $"{file}: a column's width {column.Width} is not 1 to {MostCells} blocks; made {width}, its widest widget's");
            }

            columns.Add(new CuratedColumn { Width = width, Sections = [.. sections.Select(s => Placed(s, width))] });
        }

        return layout with { Columns = columns };

        // [S-17222] Every widget at a place of its own in the column: one missing, outside it or on another moved, loudly.
        CuratedSection Placed(CuratedSection section, int width)
        {
            var cards = CuratedBento.Places(section.Widgets, width, title, CuratedFit.AsStored, out var repaired);
            for (var i = 0; i < cards.Count; i++)
            {
                var widget = section.Widgets[i];
                if (repaired.Contains(i))
                {
                    var where = widget.X is { } x && widget.Y is { } y ? $"at {x},{y} is on another widget" : "has no place";
                    Log.Warn(Scope, $"{file}: '{widget.Identity}' {where}; moved to {cards[i].X},{cards[i].Y}, the first free spot of its section");
                }
                else if (widget.X != cards[i].X)
                {
                    Log.Warn(Scope, $"{file}: '{widget.Identity}' at {widget.X},{widget.Y} runs past its column's {width} blocks; moved left to {cards[i].X}");
                }
            }

            return section with { Widgets = [.. section.Widgets.Select((w, i) => w.X == cards[i].X && w.Y == cards[i].Y ? w : w with { X = cards[i].X, Y = cards[i].Y })] };
        }
    }

    /// <summary>
    /// [S-17222] <paramref name="layout"/> with every widget's place and size as <paramref name="fit"/> lays it (titles
    /// <paramref name="title"/> rows tall): a place it has kept (held inside its column), one it lacks the first free spot,
    /// exactly where the gallery draws it. Converted so once, the shipped files of 2026-11-24 (packed in reading order until
    /// then) took the places the packer gave them on a 1920 by 1080 display: the same picture, every place written down.
    /// </summary>
    public static CuratedLayout Positioned(CuratedLayout layout, CuratedFit fit, int title)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(fit);
        title = layout.Titles ? Math.Max(0, title) : 0;
        return layout with
        {
            Columns = [.. layout.Columns.Select(c => c with
            {
                Sections = [.. c.Sections.Select(s =>
                {
                    var cards = CuratedBento.Places(s.Widgets, c.Width, title, fit, out _, repairClashes: false);
                    return s with { Widgets = [.. s.Widgets.Select((w, i) => w with { X = cards[i].X, Y = cards[i].Y, W = cards[i].W, H = cards[i].H })] };
                })],
            })],
        };
    }

    /// <summary>[S-16746] Why the build cannot show <paramref name="widget"/> (no such kind, preset or style); null when it can.</summary>
    public static string? Problem(CuratedWidget widget)
    {
        ArgumentNullException.ThrowIfNull(widget);
        if (PickOf(widget.Pick) is not { } pick)
        {
            return "no widget of the build has that id or preset";
        }

        if (widget.Style is { } style && !WidgetStyles.For(pick.Kind).Any(s => string.Equals(s.Name, style, StringComparison.OrdinalIgnoreCase)))
        {
            return $"its kind has no style '{style}'";
        }

        return null;
    }

    /// <summary>[S-16742] The pick a key names (<c>clock</c>, <c>quickToggle/wifi</c>) where the build has it; else null.</summary>
    public static WidgetPick? PickOf(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var cut = key.IndexOf(WidgetPick.Separator, StringComparison.Ordinal);
        var (id, preset) = cut < 0 ? (key, null) : (key[..cut], key[(cut + 1)..]);
        if (!WidgetId.TryParse(id, out var kind) || !ModuleCatalog.TryGet(kind, out var widget))
        {
            return null;
        }

        return preset is null || widget.Presets.Any(p => string.Equals(p.Key, preset, StringComparison.Ordinal)) ? new WidgetPick(kind, preset) : null;
    }

    /// <summary>
    /// [S-16745] A tab's automatic bento as a curated layout, its first arrangement to edit (how the shipped files began): each
    /// column on the sheet's top line a column of its width in blocks, one untitled section of its widgets in reading order at the
    /// cells their cards stand on, each at its place there ([S-17220]); a sheet in sections (Quick settings) one column of them,
    /// each under its heading's words.
    /// </summary>
    public static CuratedLayout From(GallerySheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var tiles = sheet.Tiles.Where(t => t.Piece.Pick is not null).OrderBy(t => t.Box.Y).ThenBy(t => t.Box.X).ToList();
        CuratedWidget Of(GalleryTile tile)
        {
            var card = sheet.CardOf(tile);
            var piece = tile.Piece;
            var style = piece.Style is { } value ? WidgetStyles.For(piece.Kind!.Value).FirstOrDefault(o => o.Value == value).Name : null;
            return new CuratedWidget { Pick = piece.Pick!.Value.Key, Style = style, Config = piece.Config, W = card.W, H = card.H };
        }

        // [S-17220] Its place: its tile's corner (its title over its card) from its section's room.
        CuratedWidget At(GalleryTile tile, int left, int top) => Of(tile) with { X = tile.Box.X - left, Y = tile.Box.Y - top };

        if (sheet.Headings.Count > 0)
        {
            var heads = sheet.Headings.OrderBy(h => h.Box.Y).ToList();
            return new CuratedLayout
            {
                Titles = sheet.Title > 0,
                Columns =
                [
                    new CuratedColumn
                    {
                        Width = Math.Max(1, sheet.Columns.Select(c => c.W).DefaultIfEmpty(tiles.Select(t => t.Box.Right).DefaultIfEmpty(1).Max()).Max()),
                        Sections = [.. heads.Select((h, i) => new CuratedSection
                        {
                            Title = string.IsNullOrWhiteSpace(h.Text.Text()) ? null : h.Text.Text(),
                            Widgets = [.. tiles.Where(t => t.Box.Y >= h.Box.Bottom && (i + 1 == heads.Count || t.Box.Y < heads[i + 1].Box.Y)).Select(t => At(t, 0, h.Box.Bottom))],
                        })],
                    },
                ],
            };
        }

        var top = sheet.Columns.Where(c => c.Y == 0).OrderBy(c => c.X).ToList();
        if (top.Count == 0)
        {
            top = [new CellBox(0, 0, Math.Max(1, tiles.Select(t => t.Box.Right).DefaultIfEmpty(1).Max()), sheet.Rows)];
        }

        return new CuratedLayout
        {
            Titles = sheet.Title > 0,
            Columns = [.. top.Select(c => new CuratedColumn
            {
                Width = c.W,
                Sections = [new CuratedSection { Widgets = [.. tiles.Where(t => t.Box.X >= c.X && t.Box.X < c.Right).Select(t => At(t, c.X, c.Y))] }],
            })],
        };
    }

    /// <summary>[S-16745] Every widget the layouts place, by what it is (<see cref="CuratedWidget.Identity"/>).</summary>
    public static IReadOnlySet<string> Placed(IEnumerable<CuratedLayout> layouts)
        => layouts.SelectMany(l => l.Columns).SelectMany(c => c.Sections).SelectMany(s => s.Widgets).Select(w => w.Identity).ToHashSet(StringComparer.Ordinal);
}
