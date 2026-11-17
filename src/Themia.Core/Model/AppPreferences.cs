using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-702] How the app behaves, as opposed to how widgets look (<see cref="DesignState"/>).
/// <para>
/// Kept apart from the design so an appearance preset or a design reset can never switch off, say,
/// game mode by accident.
/// </para>
/// </summary>
public sealed class AppPreferences
{
    public AppLanguage Language { get; set; } = AppLanguage.System;

    /// <summary>Hide the widgets while a full-screen game or presentation has the screen.</summary>
    public bool GameModeSuspend { get; set; }


    /// <summary>
    /// [S-5593] The desktop's icon size, Explorer's View › Large, Medium and Small icons: one size for
    /// every desktop icon on every display, never an icon's own. Medium by default, as Explorer's.
    /// </summary>
    public DesktopIconSize DesktopIconSize { get; set; } = DesktopIconSize.Medium;

    /// <summary>
    /// [S-12950] Lock widgets: outside edit mode a widget is not moved by a drag, only desktop icons are
    /// (<see cref="Themia.Core.Desktop.WidgetLock"/>). Off by default; edit mode moves everything either way.
    /// </summary>
    public bool LockWidgets { get; set; }


    /// <summary>[S-3770] How every widget writes times and dates and measures temperature.</summary>
    public RegionPreferences Region { get; set; } = new();

    /// <summary>[S-3780] How often the widgets that ask an online service ask it.</summary>
    public RefreshPreferences Refresh { get; set; } = RefreshPreferences.WithDefaults();

    /// <summary>
    /// [S-9960] The assistants' server (MCP) is on: off by default, and only ⚙ › AI assistants turns
    /// it on. This PC's own choice: an imported file never switches it ([S-498]).
    /// </summary>
    public bool McpServer { get; set; }

    /// <summary>
    /// [S-1575] The colour picker's recent colours: what was last kept from its mixer, its hex box or
    /// the eyedropper, newest first, at most one line (<see cref="Themia.Core.Settings.ColourPicker.MostRecent"/>),
    /// <c>#RRGGBB</c>. A memory of this PC's, not a look: one list for every colour row.
    /// </summary>
    public List<string> RecentColours { get; set; } = [];

    public AppPreferences Clone() => new()
    {
        Language = Language,
        GameModeSuspend = GameModeSuspend,
        DesktopIconSize = DesktopIconSize,
        LockWidgets = LockWidgets,
        Region = Region.Clone(),
        Refresh = Refresh.Clone(),
        McpServer = McpServer,
        RecentColours = [.. RecentColours],
    };
}
