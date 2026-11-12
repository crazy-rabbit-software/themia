namespace Themia.Sdk.Samples;

/// <summary>One of Robin's errands: its title and, when it has one, the day it is due, in days from today.</summary>
public sealed record SampleTask(string Title, int? DueInDays = null);

/// <summary>
/// The sample day ([S-11850], [S-2055]): Robin's errands list, the one the tasks widget shows and whose
/// due ones the week planner puts under today: four due today (one of them Mia's birthday present),
/// one overdue, one in three days, the rest without a date.
/// </summary>
public static class SampleTasks
{
    public const string ListName = "Errands";

    public static IReadOnlyList<SampleTask> Errands { get; } =
    [
        new("Send the invoice to Northwind", 0),
        new("Buy a present for Mia", 0),
        new("Book the dentist appointment", -2),
        new("Pick up the parcel", 0),
        new("Water the plants", 0),
        new("Renew the bike insurance", 3),
        new("Reply to the landlord about the heating"),
        new("Call Grandma"),
        new("Back up the laptop"),
        new("Order printer ink"),
    ];

    /// <summary>The errands due by today, overdue first: what the week planner lists under today.</summary>
    public static IReadOnlyList<SampleTask> DueByToday { get; } = [.. Errands.Where(t => t.DueInDays <= 0).OrderBy(t => t.DueInDays)];
}
