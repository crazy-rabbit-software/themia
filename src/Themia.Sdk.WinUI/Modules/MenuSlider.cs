namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-3731] An inline slider on the properties menu (a fill's Opacity, the card's Scale, a widget's
/// own number set by feel): one 32-DIP line of its words, the slider and the value as
/// <see cref="Format"/> writes it. While it is dragged the value is only previewed
/// (<see cref="Preview"/>: the line's words, and the widgets where the line says how); the release
/// stores it once (<see cref="Commit"/>). A key steps it and stores each step. The rules are here, so
/// a module's tests can drag one without a window.
/// </summary>
public sealed class MenuSlider
{
    private readonly Func<double, string> _format;
    private readonly Action<double> _set;
    private readonly Action<double?>? _preview;

    /// <param name="value">What it holds now; null for a selection whose values differ (it stands at <paramref name="min"/>, its words a dash).</param>
    /// <param name="min">The lowest value.</param>
    /// <param name="max">The highest value.</param>
    /// <param name="step">The grain every value is rounded to, from <paramref name="min"/>.</param>
    /// <param name="format">How a value is written at the line's end.</param>
    /// <param name="set">Stores a value (and saves): called once per release or key step, never for the value that holds.</param>
    /// <param name="preview">Shows a value without storing it while it is dragged, then null when the drag ends; none for a line that only previews its words.</param>
    public MenuSlider(double? value, double min, double max, double step, Func<double, string> format, Action<double> set, Action<double?>? preview = null)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(set);
        if (!(step > 0) || !(max >= min) || !double.IsFinite(min) || !double.IsFinite(max))
        {
            throw new ArgumentOutOfRangeException(nameof(step), "a slider needs a positive step and min ≤ max");
        }

        Min = min;
        Max = max;
        Step = step;
        _format = format;
        _set = set;
        _preview = preview;
        Value = value is { } v ? Snap(v) : null;
    }

    /// <summary>What it holds, on its grain; null while the selection's values differ.</summary>
    public double? Value { get; }

    public double Min { get; }

    public double Max { get; }

    public double Step { get; }

    /// <summary>A value as the line writes it.</summary>
    public string Format(double value) => _format(Snap(value));

    /// <summary>Inside the range and on the grain from <see cref="Min"/>; anything not a number is <see cref="Min"/>.</summary>
    public double Snap(double value)
    {
        if (!double.IsFinite(value))
        {
            return Min;
        }

        var stepped = Min + (Math.Round((Math.Clamp(value, Min, Max) - Min) / Step, MidpointRounding.AwayFromZero) * Step);

        // Rounded to 10 places so 0.9 is 0.9 and a stored value compares equal to the step it is.
        return Math.Round(Math.Clamp(stepped, Min, Max), 10);
    }

    /// <summary>While it is dragged: show <paramref name="value"/> (snapped) without storing it.</summary>
    public void Preview(double value) => _preview?.Invoke(Snap(value));

    /// <summary>
    /// The drag's release or a key's step: store <paramref name="value"/> (snapped) unless it is what
    /// holds, then end the preview. True when it stored.
    /// </summary>
    public bool Commit(double value)
    {
        var snapped = Snap(value);
        var changed = Value is not { } held || Math.Abs(held - snapped) > Step / 1000;
        if (changed)
        {
            _set(snapped);
        }

        _preview?.Invoke(null);
        return changed;
    }

    /// <summary>A drag that ended without a release that counts (the pointer taken away): nothing stored, the preview ended.</summary>
    public void Cancel() => _preview?.Invoke(null);
}

/// <summary>
/// [S-3732] An item line of an ordered list on the properties menu: its place in the list and what a
/// drop or Alt+Shift+Up / Down does. The host lets the lines of one list (they stand together) be
/// dragged up and down among themselves; <see cref="Move"/> moves the item and saves, once.
/// </summary>
/// <param name="Index">The item's place as the lines were built.</param>
/// <param name="Count">How many items the list has.</param>
/// <param name="Move">Moves the item at the first index to the second and saves; nothing for the same place or one outside the list.</param>
public sealed record MenuOrder(int Index, int Count, Action<int, int> Move);
