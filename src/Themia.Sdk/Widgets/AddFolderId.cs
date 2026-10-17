namespace Themia.Core.Widgets;

/// <summary>
/// [S-3510] Every folder of the add menu's tree, by name: the two top columns, the subjects under
/// Browse, and the families inside a subject. A closed set, so a folder's words and a debug render's
/// <c>--open</c> name one that exists.
/// </summary>
public enum AddFolderId
{
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

    /// <summary>[S-3511] What the tree leaves out stands here meanwhile, at Browse's end: never missing.</summary>
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
