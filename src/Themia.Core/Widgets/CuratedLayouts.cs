using System.Text.Json;
using Themia.Core.Diagnostics;
using Themia.Sdk;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-16746] The gallery's layout files: one per tab, <c>src/Themia.Core/Widgets/GalleryLayouts/&lt;tab&gt;.json</c> in the
/// repo, built into Core (owner, 2026-11-23: "In the repo, shipped": every user sees the curated gallery, the owner's edits a
/// change to commit). Read from the build (<see cref="Load"/>), checked where read (<see cref="Checked"/>: a widget the build
/// does not have, a preset or style its kind does not have, dropped loudly; a size or share out of range repaired, loudly),
/// written as the editor saves them (<see cref="Write"/>: stable, two spaces, LF, shares to four places).
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
    /// [S-16746] <paramref name="tab"/>'s layout as the build ships it, checked; null where it has none (the tab falls back to
    /// the automatic bento until curated), or where its file cannot be read (logged: the same fallback).
    /// </summary>
    public static CuratedLayout? Load(AddFolderId tab)
    {
        using var stream = typeof(CuratedLayouts).Assembly.GetManifestResourceStream(Resource + FileName(tab));
        if (stream is null)
        {
            return null;
        }

        try
        {
            using var reader = new StreamReader(stream);
            return Checked(Read(reader.ReadToEnd()), FileName(tab));
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

    /// <summary>[S-16746] A layout as its file holds it: two spaces, LF line ends, a line end last, each share to four places.</summary>
    public static string Write(CuratedLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var rounded = layout with { Columns = [.. layout.Columns.Select(c => c with { Share = Math.Round(c.Share, 4) })] };
        return JsonSerializer.Serialize(rounded, CuratedLayoutJson.Default.CuratedLayout).ReplaceLineEndings("\n") + "\n";
    }

    /// <summary>
    /// [S-16746] <paramref name="layout"/> checked (<paramref name="file"/> names it in the log): a widget whose
    /// <paramref name="problem"/> is not null (the build's registry by default, <see cref="Problem"/>) dropped, a size outside 1 to
    /// <see cref="MostCells"/> held to it, a share that is no positive number made 1; each logged. Never half-built.
    /// </summary>
    public static CuratedLayout Checked(CuratedLayout layout, string file, Func<CuratedWidget, string?>? problem = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        problem ??= Problem;
        var columns = new List<CuratedColumn>();
        foreach (var column in layout.Columns ?? [])
        {
            var share = column.Share;
            if (!double.IsFinite(share) || share <= 0)
            {
                Log.Warn(Scope, $"{file}: a column's share {share} is no positive number; made 1");
                share = 1;
            }

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

            columns.Add(new CuratedColumn { Share = share, Sections = sections });
        }

        return layout with { Columns = columns };
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
    /// column on the sheet's top line a column of that share, one untitled section of its widgets in reading order at the
    /// cells their cards stand on; a sheet in sections (Quick settings) one column of them, each under its heading's words.
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
                        Sections = [.. heads.Select((h, i) => new CuratedSection
                        {
                            Title = string.IsNullOrWhiteSpace(h.Text.Text()) ? null : h.Text.Text(),
                            Widgets = [.. tiles.Where(t => t.Box.Y >= h.Box.Bottom && (i + 1 == heads.Count || t.Box.Y < heads[i + 1].Box.Y)).Select(Of)],
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

        double across = top.Sum(c => c.W);
        return new CuratedLayout
        {
            Titles = sheet.Title > 0,
            Columns = [.. top.Select(c => new CuratedColumn
            {
                Share = Math.Round(c.W / across, 4),
                Sections = [new CuratedSection { Widgets = [.. tiles.Where(t => t.Box.X >= c.X && t.Box.X < c.Right).Select(Of)] }],
            })],
        };
    }

    /// <summary>[S-16745] Every widget the layouts place, by what it is (<see cref="CuratedWidget.Identity"/>).</summary>
    public static IReadOnlySet<string> Placed(IEnumerable<CuratedLayout> layouts)
        => layouts.SelectMany(l => l.Columns).SelectMany(c => c.Sections).SelectMany(s => s.Widgets).Select(w => w.Identity).ToHashSet(StringComparer.Ordinal);
}
