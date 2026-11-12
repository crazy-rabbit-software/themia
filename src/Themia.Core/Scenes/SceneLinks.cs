using System.Globalization;
using Themia.Core.Links;

namespace Themia.Core.Scenes;

/// <summary>
/// [S-11909] A day a scene points at or picks: a date as written (<c>2026-11-13</c>), or one counted from
/// the day the picture is drawn (<c>today</c>, <c>today+1</c>, <c>today-2</c>), so a scene over sample data
/// that follows the clock still lands on it.
/// </summary>
public readonly record struct SceneDay(DateOnly? Date, int FromToday)
{
    /// <summary>The day on <paramref name="today"/>.</summary>
    public DateOnly On(DateOnly today) => Date ?? today.AddDays(FromToday);

    /// <summary>A day as a scene writes it, or null when it is not one.</summary>
    public static SceneDay? Parse(string text)
    {
        var trimmed = text.Trim();
        if (DateOnly.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return new SceneDay(date, 0);
        }

        if (!trimmed.StartsWith("today", StringComparison.Ordinal))
        {
            return null;
        }

        var rest = trimmed["today".Length..];
        if (rest.Length == 0)
        {
            return new SceneDay(null, 0);
        }

        return rest[0] is '+' or '-' && int.TryParse(rest, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var days) && Math.Abs(days) <= 366
            ? new SceneDay(null, days)
            : null;
    }
}

/// <summary>
/// [S-11909] A moment a scene points at on the history charts: how long before the picture is drawn
/// (<c>-00:00:30</c>) or a moment as written (ISO 8601), and the subject whose reading shows its value then
/// (the subject as the charts word it: <c>cpu</c>, <c>stock/msft</c>; empty: the
/// charts' marker alone).
/// </summary>
public sealed record SceneInstant(TimeSpan? Before, DateTimeOffset? At, string Subject)
{
    /// <summary>The moment when the picture is drawn at <paramref name="now"/>.</summary>
    public DateTimeOffset On(DateTimeOffset now) => At ?? now - (Before ?? TimeSpan.Zero);

    /// <summary>A moment as a scene writes it, or null when it is not one.</summary>
    public static SceneInstant? Parse(string text, string subject)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith('-') && TimeSpan.TryParseExact(trimmed[1..], @"hh\:mm\:ss", CultureInfo.InvariantCulture, out var before))
        {
            return new SceneInstant(before, null, subject);
        }

        return DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var at) && trimmed.Contains('T')
            ? new SceneInstant(null, at, subject)
            : null;
    }
}

/// <summary>
/// [S-11937] A calendar event a scene points at, keyed as the calendar widgets key it
/// (<see cref="Themia.Core.Office.CalendarShare.Key"/>): its account, its calendar (null: the account's
/// default one) and the provider's id.
/// </summary>
public sealed record SceneEvent(string Account, string? Calendar, string Id);

/// <summary>
/// [S-11909] What a scene's picture is drawn pointing at and having picked: each a view-links value, set on
/// the scene's one board after its widgets have drawn and before the picture is taken, so every widget of
/// the scene that follows the topic shows it. Every value is keyed as the widgets key theirs (<see cref="LinkKeys"/>).
/// </summary>
public sealed record SceneLinks(
    SceneDay? HoveredDate = null,
    SceneDay? SelectedDate = null,
    string? HoveredApp = null,
    string? SelectedApp = null,
    SceneInstant? HoveredInstant = null,
    string? HoveredPerson = null,
    string? HoveredFile = null,
    string? HoveredDrive = null,
    string? HoveredDevice = null,
    SceneEvent? HoveredEvent = null)
{
    /// <summary>The property names a scene's <c>links</c> may carry.</summary>
    public static IReadOnlyList<string> Names { get; } =
        ["hoveredDate", "selectedDate", "hoveredApp", "selectedApp", "hoveredInstant", "hoveredPerson", "hoveredFile", "hoveredDrive", "hoveredDevice", "hoveredEvent"];

    /// <summary>Set every value on the board through <paramref name="director"/>, a member of its own (so every widget hears them).</summary>
    public void Apply(LinkMember director, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(director);
        var today = DateOnly.FromDateTime(now.LocalDateTime);

        Put(director, LinkTopics.SelectedDate, SelectedDate is { } picked ? new DateLink(picked.On(today)) : null);
        Put(director, LinkTopics.SelectedApp, LinkKeys.AppOfProcess(SelectedApp) is { } app ? new AppLink(app) : null);
        Put(director, LinkTopics.HoveredDate, HoveredDate is { } day ? new DateLink(day.On(today)) : null);
        Put(director, LinkTopics.HoveredApp, LinkKeys.AppOfProcess(HoveredApp) is { } hovered ? new AppLink(hovered) : null);
        Put(director, LinkTopics.HoveredInstant, HoveredInstant is { } instant ? new InstantLink(instant.On(now), instant.Subject) : null);
        Put(director, LinkTopics.HoveredPerson, LinkKeys.PersonLink(HoveredPerson, HoveredPerson));
        Put(director, LinkTopics.HoveredFile, LinkKeys.FileLink(HoveredFile));
        Put(director, LinkTopics.HoveredDrive, LinkKeys.DriveLink(HoveredDrive));
        Put(director, LinkTopics.HoveredDevice, LinkKeys.DeviceLink(HoveredDevice));
        Put(director, LinkTopics.HoveredEvent, HoveredEvent is { } e ? new EventLink(e.Account, e.Calendar, e.Id) : null);
    }

    private static void Put<T>(LinkMember director, LinkTopic<T> topic, T? value)
        where T : class, IEquatable<T>
    {
        if (value is not null)
        {
            director.Set(topic, value);
        }
    }
}
