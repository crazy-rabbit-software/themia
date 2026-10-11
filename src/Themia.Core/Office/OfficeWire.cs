using System.Text.Json.Serialization;

namespace Themia.Core.Office;

// The Microsoft Graph and Google shapes the calendar and contacts widgets read, as source-generated
// contracts (Native AOT has no reflection serializer), and the one place each becomes a model
// record. Every field is optional: a provider that leaves one out costs that field, not the item.

/// <summary>A Graph <c>dateTimeTimeZone</c>.</summary>
public sealed class GraphDateTime
{
    [JsonPropertyName("dateTime")]
    public string? DateTime { get; set; }

    [JsonPropertyName("timeZone")]
    public string? TimeZone { get; set; }
}

public sealed class GraphEmailAddress
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }
}

public sealed class GraphRecipient
{
    [JsonPropertyName("emailAddress")]
    public GraphEmailAddress? EmailAddress { get; set; }
}

public sealed class GraphLocation
{
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }
}

/// <summary>One event of a Graph <c>calendarView</c>.</summary>
public sealed class GraphEvent
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("subject")]
    public string? Subject { get; set; }

    [JsonPropertyName("start")]
    public GraphDateTime? Start { get; set; }

    [JsonPropertyName("end")]
    public GraphDateTime? End { get; set; }

    [JsonPropertyName("isAllDay")]
    public bool IsAllDay { get; set; }

    [JsonPropertyName("isCancelled")]
    public bool IsCancelled { get; set; }

    [JsonPropertyName("location")]
    public GraphLocation? Location { get; set; }

    [JsonPropertyName("organizer")]
    public GraphRecipient? Organizer { get; set; }

    [JsonPropertyName("webLink")]
    public string? WebLink { get; set; }
}

/// <summary>One entry of Graph <c>/me/calendars</c>.</summary>
public sealed class GraphCalendar
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("isDefaultCalendar")]
    public bool IsDefaultCalendar { get; set; }

    [JsonPropertyName("hexColor")]
    public string? HexColor { get; set; }
}

/// <summary>One entry of Graph <c>/me/contacts</c>.</summary>
public sealed class GraphContact
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("emailAddresses")]
    public List<GraphEmailAddress?>? EmailAddresses { get; set; }

    [JsonPropertyName("businessPhones")]
    public List<string?>? BusinessPhones { get; set; }

    [JsonPropertyName("mobilePhone")]
    public string? MobilePhone { get; set; }

    [JsonPropertyName("homePhones")]
    public List<string?>? HomePhones { get; set; }

    [JsonPropertyName("jobTitle")]
    public string? JobTitle { get; set; }

    [JsonPropertyName("companyName")]
    public string? CompanyName { get; set; }

    [JsonPropertyName("birthday")]
    public string? Birthday { get; set; }

    /// <summary>The folder it lives in: the default contacts folder, whose delta round [S-2685] follows.</summary>
    [JsonPropertyName("parentFolderId")]
    public string? ParentFolderId { get; set; }
}

/// <summary>A Google Calendar <c>EventDateTime</c>: a date for all-day events, a dateTime otherwise.</summary>
public sealed class GoogleEventTime
{
    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("dateTime")]
    public string? DateTime { get; set; }
}

public sealed class GooglePerson
{
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }
}

/// <summary>One item of Google Calendar <c>events.list</c>.</summary>
public sealed class GoogleEvent
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("htmlLink")]
    public string? HtmlLink { get; set; }

    [JsonPropertyName("start")]
    public GoogleEventTime? Start { get; set; }

    [JsonPropertyName("end")]
    public GoogleEventTime? End { get; set; }

    [JsonPropertyName("organizer")]
    public GooglePerson? Organizer { get; set; }
}

/// <summary>One item of Google Calendar <c>calendarList.list</c>.</summary>
public sealed class GoogleCalendar
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("summaryOverride")]
    public string? SummaryOverride { get; set; }

    [JsonPropertyName("primary")]
    public bool Primary { get; set; }

    [JsonPropertyName("backgroundColor")]
    public string? BackgroundColor { get; set; }
}

public sealed class GoogleName
{
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }
}

public sealed class GoogleValue
{
    [JsonPropertyName("value")]
    public string? Value { get; set; }
}

public sealed class GoogleOrganization
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }
}

public sealed class GoogleDate
{
    [JsonPropertyName("year")]
    public int? Year { get; set; }

    [JsonPropertyName("month")]
    public int? Month { get; set; }

    [JsonPropertyName("day")]
    public int? Day { get; set; }
}

public sealed class GoogleBirthday
{
    [JsonPropertyName("date")]
    public GoogleDate? Date { get; set; }
}

/// <summary>One connection of Google People <c>people/me/connections</c>.</summary>
public sealed class GooglePersonRecord
{
    [JsonPropertyName("resourceName")]
    public string? ResourceName { get; set; }

    [JsonPropertyName("names")]
    public List<GoogleName?>? Names { get; set; }

    [JsonPropertyName("emailAddresses")]
    public List<GoogleValue?>? EmailAddresses { get; set; }

    [JsonPropertyName("phoneNumbers")]
    public List<GoogleValue?>? PhoneNumbers { get; set; }

    [JsonPropertyName("organizations")]
    public List<GoogleOrganization?>? Organizations { get; set; }

    [JsonPropertyName("birthdays")]
    public List<GoogleBirthday?>? Birthdays { get; set; }
}

/// <summary>Source-generated contracts for the Graph and Google items above.</summary>
[JsonSerializable(typeof(GraphEvent))]
[JsonSerializable(typeof(GraphCalendar))]
[JsonSerializable(typeof(GraphContact))]
[JsonSerializable(typeof(GoogleEvent))]
[JsonSerializable(typeof(GoogleCalendar))]
[JsonSerializable(typeof(GooglePersonRecord))]
public sealed partial class OfficeJsonContext : JsonSerializerContext;

/// <summary>[S-565] Graph and Google items to model records. Null for an item that is not usable.</summary>
public static class OfficeWire
{
    /// <summary>
    /// A Graph event. Times arrive zoneless in UTC (the request asks for
    /// <c>outlook.timezone="UTC"</c>); an all-day event's date is the date part, kept as a date.
    /// Cancelled occurrences are not events ([S-185]).
    /// </summary>
    public static CalendarEvent? FromGraph(GraphEvent item)
    {
        if (item.IsCancelled || string.IsNullOrWhiteSpace(item.Id))
        {
            return null;
        }

        CalendarEvent? result;

        if (item.IsAllDay)
        {
            if (CalendarRules.ParseDate(item.Start?.DateTime) is not { } first)
            {
                return null;
            }

            var end = CalendarRules.ParseDate(item.End?.DateTime) ?? first.AddDays(1);
            result = CalendarEvent.AllDayEvent(item.Id, item.Subject ?? string.Empty, first, end);
        }
        else
        {
            if (CalendarRules.ParseDateTime(item.Start?.DateTime) is not { } start)
            {
                return null;
            }

            var end = CalendarRules.ParseDateTime(item.End?.DateTime) ?? start;
            result = new CalendarEvent { Id = item.Id, Subject = item.Subject ?? string.Empty, Start = start, End = end };
        }

        return result with
        {
            Location = Blank(item.Location?.DisplayName),
            Organizer = Blank(item.Organizer?.EmailAddress?.Name) ?? Blank(item.Organizer?.EmailAddress?.Address),
            WebLink = Blank(item.WebLink),
        };
    }

    /// <summary>
    /// A Google event: <c>start.date</c> means all-day (with an exclusive <c>end.date</c>),
    /// <c>start.dateTime</c> carries its own offset — negative for the Americas ([S-565]).
    /// </summary>
    public static CalendarEvent? FromGoogle(GoogleEvent item)
    {
        if (string.IsNullOrWhiteSpace(item.Id) || string.Equals(item.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        CalendarEvent result;

        if (CalendarRules.ParseDate(item.Start?.Date) is { } first && item.Start?.DateTime is null)
        {
            var end = CalendarRules.ParseDate(item.End?.Date) ?? first.AddDays(1);
            result = CalendarEvent.AllDayEvent(item.Id, item.Summary ?? string.Empty, first, end);
        }
        else if (CalendarRules.ParseDateTime(item.Start?.DateTime) is { } start)
        {
            var end = CalendarRules.ParseDateTime(item.End?.DateTime) ?? start;
            result = new CalendarEvent { Id = item.Id, Subject = item.Summary ?? string.Empty, Start = start, End = end };
        }
        else
        {
            return null;
        }

        return result with
        {
            Location = Blank(item.Location),
            Organizer = Blank(item.Organizer?.DisplayName) ?? Blank(item.Organizer?.Email),
            WebLink = Blank(item.HtmlLink),
        };
    }

    public static CalendarInfo? FromGraph(GraphCalendar item)
        => string.IsNullOrWhiteSpace(item.Id)
            ? null
            : new CalendarInfo(item.Id, Blank(item.Name) ?? "Calendar", item.IsDefaultCalendar, CalendarRules.NormalizeColor(item.HexColor));

    public static CalendarInfo? FromGoogle(GoogleCalendar item)
        => string.IsNullOrWhiteSpace(item.Id)
            ? null
            : new CalendarInfo(
                item.Id,
                Blank(item.SummaryOverride) ?? Blank(item.Summary) ?? item.Id,
                item.Primary,
                CalendarRules.NormalizeColor(item.BackgroundColor));

    /// <summary>A Graph contact: the first address, the first business phone else the mobile else home.</summary>
    public static Contact? FromGraph(GraphContact item)
    {
        if (Blank(item.DisplayName) is not { } name)
        {
            return null;
        }

        var phone = FirstOf(item.BusinessPhones) ?? Blank(item.MobilePhone) ?? FirstOf(item.HomePhones);

        return new Contact
        {
            Id = item.Id ?? name,
            Name = name,
            Email = item.EmailAddresses?.Select(e => Blank(e?.Address)).FirstOrDefault(a => a is not null),
            Phone = phone,
            JobTitle = Blank(item.JobTitle),
            Company = Blank(item.CompanyName),
        };
    }

    public static Contact? FromGoogle(GooglePersonRecord item)
    {
        if (item.Names?.Select(n => Blank(n?.DisplayName)).FirstOrDefault(n => n is not null) is not { } name)
        {
            return null;
        }

        var org = item.Organizations?.FirstOrDefault(o => o is not null);

        return new Contact
        {
            Id = item.ResourceName ?? name,
            Name = name,
            Email = item.EmailAddresses?.Select(e => Blank(e?.Value)).FirstOrDefault(v => v is not null),
            Phone = item.PhoneNumbers?.Select(p => Blank(p?.Value)).FirstOrDefault(v => v is not null),
            JobTitle = Blank(org?.Title),
            Company = Blank(org?.Name),
        };
    }

    /// <summary>[S-575] A Graph contact's birthday (<c>1990-05-10T…</c>), or null.</summary>
    public static Birthday? BirthdayFromGraph(GraphContact item) => BirthdayRules.Parse(item.DisplayName, item.Birthday);

    /// <summary>[S-575] A Google person's first usable birthday, or null.</summary>
    public static Birthday? BirthdayFromGoogle(GooglePersonRecord item)
    {
        var name = item.Names?.Select(n => Blank(n?.DisplayName)).FirstOrDefault(n => n is not null);

        return item.Birthdays?
            .Select(b => b?.Date is { } d ? BirthdayRules.From(name, d.Year, d.Month, d.Day) : null)
            .FirstOrDefault(b => b is not null);
    }

    private static string? FirstOf(List<string?>? values) => values?.Select(Blank).FirstOrDefault(v => v is not null);

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
