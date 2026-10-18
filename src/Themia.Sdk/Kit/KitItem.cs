namespace Themia.Sdk.Kit;

/// <summary>
/// [S-7000] One row's worth of a source's data, already validated and cut to its bounds
/// (<see cref="KitItems.Clean"/>): the one shape every list widget built on the kit draws. Plain
/// text only: a title is never markup, a link is never a command.
/// </summary>
/// <param name="Id">Stable across refreshes: the keyboard cursor and the menus follow it.</param>
/// <param name="Title">The headline; empty is the spec's "Untitled" sentence.</param>
/// <param name="Subtitle">One line: the site, the sender, the repository.</param>
/// <param name="Preview">Wraps to the plan's preview lines.</param>
/// <param name="Lead">What leads the row (an icon, the site's own icon, a dot).</param>
/// <param name="When">Drawn per the spec's <see cref="KitTime"/>.</param>
/// <param name="Tone">The row's accent: unread, failing, overdue.</param>
/// <param name="Emphasis">Unread or "needs you": the title in the strong body style.</param>
/// <param name="Badges">Short labels after the title ("Draft", "3").</param>
/// <param name="Link">The primary open target: a web address the item rules accepted.</param>
/// <param name="Group">The group the row is in; its heading is drawn by the kit.</param>
public sealed record KitItem(
    string Id,
    string Title,
    string? Subtitle = null,
    string? Preview = null,
    KitLead Lead = default,
    KitWhen? When = null,
    KitTone Tone = KitTone.Neutral,
    bool Emphasis = false,
    IReadOnlyList<KitBadge>? Badges = null,
    Uri? Link = null,
    KitGroup? Group = null);

/// <summary>What leads a row. <see cref="KitLeadKind.Icon"/> names a <see cref="KitIcon"/>; <see cref="KitLeadKind.SiteIcon"/> a web address whose site's own icon it is.</summary>
public readonly record struct KitLead(KitLeadKind Kind, string? Value = null, KitTone Tone = KitTone.Neutral)
{
    public static KitLead None => default;

    /// <summary>[S-1304] The site's own icon for <paramref name="link"/> (a globe until it arrives, or when the site has none).</summary>
    public static KitLead Site(Uri link) => new(KitLeadKind.SiteIcon, link.AbsoluteUri);
}

public enum KitLeadKind
{
    None,
    Icon,
    Initials,
    SiteIcon,
    StatusDot,
}

/// <summary>A short label after the title: "Draft", a count, a label's name.</summary>
public readonly record struct KitBadge(string Text, KitTone Tone = KitTone.Neutral);

/// <summary>The accents a kit row may carry; the App maps each to a theme brush, never a colour.</summary>
public enum KitTone
{
    Neutral,
    Accent,
    Success,
    Caution,
    Critical,
}

/// <summary>How an item's <see cref="KitWhen"/> is written (always through <see cref="Localization.LocaleFormat"/>).</summary>
public enum KitTime
{
    None,

    /// <summary>[S-1302] "Just now", "5 min. ago" for a week, then the date ("Mar 4", with the year once it is not this year's).</summary>
    Age,

    /// <summary>The clock time ("10:42").</summary>
    Clock,

    /// <summary>The day ("Mar 4").</summary>
    Date,
}

/// <summary>
/// [S-7001] An instant, or a day without a time (a due date, an all-day event): exactly one of the
/// two. A day is never turned into midnight UTC, so "due today" does not move with the time zone.
/// </summary>
public readonly record struct KitWhen
{
    private KitWhen(DateTimeOffset? at, DateOnly? day)
    {
        At = at;
        Day = day;
    }

    public DateTimeOffset? At { get; }

    public DateOnly? Day { get; }

    public static KitWhen Instant(DateTimeOffset at) => new(at, null);

    public static KitWhen OnDay(DateOnly day) => new(null, day);
}

/// <summary>
/// A group: a day (its heading written by the kit) or a named key whose heading the rules give (a
/// repository, a project).
/// </summary>
public readonly record struct KitGroup(string Key, string? Heading = null, DateOnly? Day = null);

/// <summary>
/// The line above a list ("main · checks passing"). <paramref name="Fraction"/> draws the thin
/// progress band under it; never a control.
/// </summary>
public sealed record KitSummary(string Text, string? Detail = null, double? Fraction = null, KitTone Tone = KitTone.Neutral);

/// <summary>
/// The glyphs a kit item or action may name. A closed set: the App maps each member to a
/// <c>Glyphs</c> entry in one switch (<c>KitIcons</c>), and a test fails on a member without one.
/// </summary>
public enum KitIcon
{
    /// <summary>The action kind's own glyph.</summary>
    Default,
    Open,
    Web,
    Link,
    Copy,
    Refresh,
    Rename,
    Delete,
    Check,
    Feed,
    Mail,
    Task,
    Event,
    Warning,
}
