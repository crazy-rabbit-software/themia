namespace Themia.Core.Model;

public sealed partial class ViewState
{
    /// <summary>
    /// [S-44] Bring a read view back inside its invariants, saying what changed: a name, an icon that is one glyph, a
    /// colour that is one, and its widgets: each with a config and an id of its own in the view, each repaired by its
    /// own rules (<see cref="WidgetInstance.Repair"/>). Its id, unique among the views, is the walk's
    /// (<see cref="Storage.StateRepair"/>).
    /// </summary>
    public IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
        {
            Name = "View";
            repairs.Add("view had no name");
        }

        if (Icon is not null && !IsGlyph(Icon))
        {
            // [S-703] Fall back to initials rather than draw whatever text got onto disk.
            Icon = null;
            repairs.Add($"view '{Id}' had an icon that is not a single glyph");
        }

        // [S-3811] A colour that does not parse draws the icon in the theme's colour, loudly.
        if (ViewOps.CleanColor(Color) is var color && color != Color)
        {
            if (color is null && !string.IsNullOrWhiteSpace(Color))
            {
                repairs.Add($"view '{Id}' had a colour that is not #RRGGBB; it uses the theme's");
            }

            Color = color;
        }

        // [S-855] An area that is no part of the screen is no area: the view settles as laid out on none, loudly.
        if (WorkArea is { } area && !(Geometry.GridGeometry.NormaliseAllowed(area) is { } clean && Geometry.WorkAreaSettle.Same(clean, area)))
        {
            WorkArea = null;
            repairs.Add($"view '{Id}' had a work area that is no part of the screen; it is settled afresh");
        }

        Widgets ??= [];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = Widgets.Count - 1; i >= 0; i--)
        {
            var widget = Widgets[i];

            // [S-6908] A widget is its config: one without a config is no widget this build knows.
            // The read never makes one (the store drops it before the typed read); this is for a
            // state built in memory.
            if (widget.Config is null)
            {
                Widgets.RemoveAt(i);
                repairs.Add($"dropped widget '{widget.Id}' without a config from view '{Name}'");
                continue;
            }

            if (string.IsNullOrWhiteSpace(widget.Id) || !seen.Add(widget.Id))
            {
                widget.Id = Guid.NewGuid().ToString("n");
                seen.Add(widget.Id);
                repairs.Add("widget had a missing or duplicate id");
            }

            repairs.AddRange(widget.Repair());
        }

        return repairs;
    }

    /// <summary>One visible glyph: a single UTF-16 unit, or one surrogate pair.</summary>
    private static bool IsGlyph(string text)
        => text.Length == 1 ? !char.IsWhiteSpace(text[0]) && !char.IsControl(text[0])
            : text.Length == 2 && char.IsSurrogatePair(text[0], text[1]);
}
