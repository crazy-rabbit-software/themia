using Themia.Sdk.Kit;

namespace Themia.Core.Links;

/// <summary>[S-11900] How one part a widget draws stands against what the view points at and has picked.</summary>
public enum LinkLight
{
    /// <summary>Not the thing pointed at or picked: drawn as it is (a row), or less while something is (a mark).</summary>
    None = 0,

    /// <summary>The thing pointed at elsewhere: a row in the item layer's hover fill, a mark whole.</summary>
    Pointed = 1,

    /// <summary>The thing picked: a row in the selected fill with the accent pill at its start, a mark whole.</summary>
    Picked = 2,
}

/// <summary>
/// [S-11900]..[S-11904] The one look of view links, for every kind: a row that is the thing lights as if
/// the pointer were on it (pointed) or as selected with the accent pill (picked) and nothing else in a list
/// changes; a chart's or a set of marks' other parts go to <see cref="FamilyShape.LinkPointed"/> or
/// <see cref="FamilyShape.LinkPicked"/>. Pointed and picked together: the picked look, the pointed part at
/// full strength too. A value the widget does not show changes nothing in it (the caller passes only what
/// lands on one of its parts).
/// </summary>
public static class LinkEmphasis
{
    /// <summary>[S-11901] How <paramref name="part"/> lights: picked wins over pointed.</summary>
    public static LinkLight Light<T>(T? part, T? pointed, T? picked)
        where T : class
    {
        if (part is null)
        {
            return LinkLight.None;
        }

        var comparer = EqualityComparer<T>.Default;
        return picked is not null && comparer.Equals(part, picked) ? LinkLight.Picked
            : pointed is not null && comparer.Equals(part, pointed) ? LinkLight.Pointed
            : LinkLight.None;
    }

    /// <summary>
    /// [S-11902] The factor on a mark's own opacity: whole when it is pointed at or picked, or while nothing
    /// is; else <see cref="FamilyShape.LinkPicked"/> while something is picked and
    /// <see cref="FamilyShape.LinkPointed"/> while something is only pointed at.
    /// </summary>
    public static double Strength<T>(T? part, T? pointed, T? picked)
        where T : class
        => Light(part, pointed, picked) != LinkLight.None ? 1 : Others(pointed is not null, picked is not null);

    /// <summary>[S-11902] How strong the parts that are not the thing stay.</summary>
    public static double Others(bool pointed, bool picked)
        => picked ? FamilyShape.LinkPicked : pointed ? FamilyShape.LinkPointed : 1;

    /// <summary>
    /// [S-11900] The value as this widget shows it: itself when it is among <paramref name="shown"/>, else
    /// null (a value the widget does not draw dims nothing in it).
    /// </summary>
    public static T? Shown<T>(T? value, IEnumerable<T> shown)
        where T : class
        => value is not null && shown.Contains(value) ? value : null;
}
