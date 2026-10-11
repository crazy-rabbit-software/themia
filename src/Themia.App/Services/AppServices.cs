namespace Themia.App.Services;

/// <summary>
/// The few services a widget view needs but cannot be handed.
/// <para>
/// Widget views are built by the catalogue from a <c>WidgetKind</c> alone, so there is no place to
/// inject into. The ticker is passed through <c>IWidgetView.Attach</c> because every widget needs
/// it; these are here because only a few widgets do, and threading them through every signature for
/// that would be worse than one explicit lookup.
/// </para>
/// <para>
/// Every one is a single shared instance, so however many mail or calendar widgets are on the
/// desktop there is one sync, one cache and one rate-limit budget per account.
/// </para>
/// </summary>
internal static class AppServices
{
    /// <summary>
    /// Themia's own accounts and every authorized request made for them — the seam mail, calendar,
    /// contacts and tasks are built on. Null before the host has
    /// started (and in a debug render). See <see cref="Accounts.AccountService"/>.
    /// </summary>
    public static Accounts.AccountService? Accounts { get; internal set; }

    /// <summary>
    /// [S-2600] The offline sync store and the widgets' needs across every view. Null before the host
    /// has started; detached (reads and writes nothing) in a debug render.
    /// </summary>
    public static Sync.SyncHost? Sync { get; internal set; }

    /// <summary>[S-2661] The feeds every Rss widget reads, kept fresh in the background. Null before the host has started.</summary>
    public static Web.FeedSync? Feeds { get; internal set; }

    /// <summary>
    /// [S-459] Where screen-time widgets read from: the host's one tracker. Null before the host has started.
    /// </summary>
    public static IScreenTimeSource? ScreenTime { get; internal set; }

    /// <summary>
    /// [S-2500] Where every system widget (meters, GPU, the detail widgets) gets its figures: the
    /// host's one <see cref="SystemData.SystemHub"/>, on the shared ticker. A debug render sets its own.
    /// Null before the host has started.
    /// </summary>
    public static SystemData.SystemHub? System { get; internal set; }

    /// <summary>
    /// [S-546] Where Mail widgets read from: the host's one <see cref="Mail.MailService"/>.
    /// Null before the host has started.
    /// </summary>
    public static Mail.IMailSource? Mail { get; internal set; }

    /// <summary>
    /// Open Settings on the Accounts page, for a widget whose empty state is "sign in". Null where
    /// there is no settings window to open (a debug render).
    /// </summary>
    public static Action? ShowAccounts { get; internal set; }

    /// <summary>
    /// [S-1411] Open the app-layout editor on the Shortcuts item with this id (item ids are unique
    /// across widgets). Null where there is no host (a debug render).
    /// </summary>
    public static Action<string>? EditAppLayout { get; internal set; }

    /// <summary>
    /// [S-591] Where Tasks widgets read and write: account task lists and the lists kept on this PC.
    /// The host's one <see cref="Tasks.TaskService"/>.
    /// </summary>
    public static Tasks.TaskService? Tasks { get; internal set; }

    /// <summary>
    /// [S-1420] The habit lists kept on this PC, shared by every Habits widget and its settings: the
    /// host's one pool.
    /// </summary>
    public static Habits.HabitPool? Habits { get; internal set; }

    /// <summary>
    /// [S-1348] The notes kept on this PC, shared by every Notes widget and its settings: the host's
    /// one pool over <c>notes\</c> beside the state file.
    /// </summary>
    public static Notes.NotePool? Notes { get; internal set; }
}
