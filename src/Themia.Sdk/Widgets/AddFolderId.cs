namespace Themia.Core.Widgets;

/// <summary>
/// [S-3510] Every folder of the add page's tree, by name: Popular and Browse, the subjects under
/// Browse (each a tab of the page, [S-11700]), and the families inside a subject. A closed set, so a
/// folder's words and a debug render's <c>--tab</c> name one that exists.
/// </summary>
public enum AddFolderId
{
    /// <summary>[S-11701] The page's first tab: the starter groups and Popular. No widget is at home here.</summary>
    Start,

    Popular,
    Browse,

    // The subjects, in Browse's order.
    Time,
    CalendarMail,
    Weather,
    Markets,
    Media,
    Notes,
    Files,
    System,
    Devices,
    QuickSettings,

    /// <summary>[S-3511] What the tree leaves out stands here meanwhile, the last tab: never missing.</summary>
    More,

    // The families: one widget in several layouts or several parts.
    Processor,
    Graphics,
    MemoryReading,
    NetworkReading,
    PingReading,
    ScreenTime,
    BatteryReading,
    PowerButtons,
}
