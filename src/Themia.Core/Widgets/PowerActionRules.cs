using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Widgets;

/// <summary>Which Win32 call carries out an action.</summary>
public enum PowerCallKind
{
    /// <summary><c>ExitWindowsEx</c>: shut down, restart, sign out.</summary>
    ExitWindows = 0,

    /// <summary><c>SetSuspendState</c>: sleep, hibernate. Blocks until the machine resumes.</summary>
    Suspend = 1,

    /// <summary><c>LockWorkStation</c>.</summary>
    Lock = 2,

    /// <summary>A <c>WM_SYSCOMMAND</c>/<c>SC_MONITORPOWER</c> broadcast.</summary>
    MonitorOff = 3,
}

/// <summary>
/// [S-1243] Exactly what the backend does for one action, decided here so it can be tested without
/// shutting anything down. The Win32 seam only executes a plan.
/// </summary>
/// <param name="Kind">Which call.</param>
/// <param name="ExitFlags"><c>EWX_*</c> flags for <see cref="PowerCallKind.ExitWindows"/>, else 0.</param>
/// <param name="Hibernate">For <see cref="PowerCallKind.Suspend"/>: hibernate rather than sleep.</param>
/// <param name="NeedsShutdownPrivilege">Enable <c>SeShutdownPrivilege</c> on the process token first.</param>
public readonly record struct PowerCall(PowerCallKind Kind, uint ExitFlags, bool Hibernate, bool NeedsShutdownPrivilege);

/// <summary>Why an action did not happen. The machine is still up, so the tile has to say.</summary>
public enum PowerFailure
{
    None = 0,

    /// <summary><c>AdjustTokenPrivileges</c> assigned nothing (<c>ERROR_NOT_ALL_ASSIGNED</c>): policy strips the privilege.</summary>
    NotAllowed = 1,

    /// <summary><c>SetSuspendState(TRUE, …)</c> refused: hibernation is off on this machine.</summary>
    HibernateUnavailable = 2,

    /// <summary><c>SetSuspendState(FALSE, …)</c> refused.</summary>
    SleepRefused = 3,

    /// <summary>Any other call failed; the Win32 error says which way.</summary>
    Win32 = 4,
}

/// <summary>The result of one action: success, or a named failure with the Win32 error behind it.</summary>
public readonly record struct PowerOutcome(PowerFailure Failure, int Win32Error)
{
    public static PowerOutcome Ok { get; } = new(PowerFailure.None, 0);

    public bool Succeeded => Failure == PowerFailure.None;

    public static PowerOutcome Error(int win32Error) => new(PowerFailure.Win32, win32Error);
}

/// <summary>
/// The power widgets' rules: which kind runs which action, which actions need a second click, what
/// each one calls, and how a failure reads. Pure, so the decisions are tested here and the Win32
/// seam stays a few lines of calls.
/// </summary>
public static class PowerActionRules
{
    // EWX_* and SHTDN_REASON_* (winuser.h / reason.h).
    public const uint ExitLogoff = 0x0;
    public const uint ExitPowerOff = 0x8;
    public const uint ExitReboot = 0x2;
    public const uint ExitForceIfHung = 0x10;

    /// <summary>
    /// <c>SHTDN_REASON_MAJOR_OTHER | SHTDN_REASON_MINOR_OTHER | SHTDN_REASON_FLAG_PLANNED</c>: the
    /// event log records a user-initiated, planned shutdown rather than an unexpected one.
    /// </summary>
    public const uint PlannedReason = 0x80000000;

    /// <summary><c>ERROR_ACCESS_DENIED</c>.</summary>
    public const int AccessDenied = 5;

    /// <summary><c>ERROR_NOT_ALL_ASSIGNED</c>: <c>AdjustTokenPrivileges</c> "succeeded" and assigned nothing.</summary>
    public const int NotAllAssigned = 1300;

    /// <summary><c>ERROR_PRIVILEGE_NOT_HELD</c>.</summary>
    public const int PrivilegeNotHeld = 1314;

    /// <summary><c>ERROR_SHUTDOWN_IN_PROGRESS</c>.</summary>
    public const int ShutdownInProgress = 1115;

    /// <summary>[S-1910] Every action, in the order the picker lists their widgets.</summary>
    public static IReadOnlyList<PowerAction> All { get; } =
    [
        PowerAction.Shutdown,
        PowerAction.Restart,
        PowerAction.Sleep,
        PowerAction.Hibernate,
        PowerAction.Lock,
        PowerAction.SignOut,
        PowerAction.ScreenOff,
    ];

    /// <summary>[S-1910] The action a widget kind stands for, or null for a kind that is not a power widget.</summary>
    public static PowerAction? ForKind(WidgetId kind) => ByKind.Value.TryGetValue(kind, out var action) ? action : null;

    /// <summary>The inverse of <see cref="KindOf"/>, built from it so the two cannot disagree.</summary>
    private static readonly Lazy<Dictionary<WidgetId, PowerAction>> ByKind = new(() => Enum.GetValues<PowerAction>().ToDictionary(KindOf));

    /// <summary>[S-1910] The widget kind of an action: the inverse of <see cref="ForKind"/>.</summary>
    public static WidgetId KindOf(PowerAction action) => action switch
    {
        PowerAction.Shutdown => ShutdownConfig.Descriptor.Id,
        PowerAction.Restart => RestartConfig.Descriptor.Id,
        PowerAction.Sleep => SleepConfig.Descriptor.Id,
        PowerAction.Hibernate => HibernateConfig.Descriptor.Id,
        PowerAction.Lock => LockConfig.Descriptor.Id,
        PowerAction.SignOut => SignOutConfig.Descriptor.Id,
        PowerAction.ScreenOff => ScreenOffConfig.Descriptor.Id,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "not a power action"),
    };

    /// <summary>
    /// [S-1913] The actions a stray click must not run: shut down, restart and sign out close every
    /// app, so unsaved work can be lost; hibernate powers the machine off. They arm on the first
    /// click and run on the second. Sleep, lock and display-off come back to the same desktop with a
    /// keypress, so they run at once.
    /// </summary>
    public static bool NeedsArming(PowerAction action)
        => action is PowerAction.Shutdown or PowerAction.Restart or PowerAction.SignOut or PowerAction.Hibernate;

    /// <summary>
    /// [S-1914] Whether the action can run on this machine. Only hibernation depends on the machine:
    /// it is off when Windows has no hibernation file (<c>powercfg /h off</c>) or policy forbids it.
    /// </summary>
    public static bool Available(PowerAction action, bool canHibernate)
        => action != PowerAction.Hibernate || canHibernate;

    /// <summary>[S-1243] The call behind an action. Mirrors the original backend exactly.</summary>
    public static PowerCall Plan(PowerAction action) => action switch
    {
        // EWX_FORCEIFHUNG overrides only apps that stopped answering; a responsive app with unsaved
        // work still gets to block, as the Start menu's own entries do.
        PowerAction.Shutdown => new(PowerCallKind.ExitWindows, ExitPowerOff | ExitForceIfHung, false, true),
        PowerAction.Restart => new(PowerCallKind.ExitWindows, ExitReboot | ExitForceIfHung, false, true),

        // Signing out ends only this user's session: no privilege needed.
        PowerAction.SignOut => new(PowerCallKind.ExitWindows, ExitLogoff | ExitForceIfHung, false, false),
        PowerAction.Sleep => new(PowerCallKind.Suspend, 0, false, true),
        PowerAction.Hibernate => new(PowerCallKind.Suspend, 0, true, true),
        PowerAction.Lock => new(PowerCallKind.Lock, 0, false, false),
        PowerAction.ScreenOff => new(PowerCallKind.MonitorOff, 0, false, false),
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "not a power action"),
    };

    /// <summary>
    /// [S-1243] What a privilege adjustment's last error means. <c>AdjustTokenPrivileges</c> returns
    /// success even when it assigned nothing, so a zero return is not enough: without this check the
    /// caller would go on to a shutdown that quietly does nothing.
    /// </summary>
    public static PowerOutcome PrivilegeOutcome(bool adjusted, int lastError)
    {
        if (!adjusted)
        {
            return lastError is NotAllAssigned or PrivilegeNotHeld
                ? new PowerOutcome(PowerFailure.NotAllowed, lastError)
                : PowerOutcome.Error(lastError);
        }

        return lastError is NotAllAssigned
            ? new PowerOutcome(PowerFailure.NotAllowed, lastError)
            : PowerOutcome.Ok;
    }

    /// <summary>[S-1243] What a <c>SetSuspendState</c> refusal means, by which way it was asked.</summary>
    public static PowerOutcome SuspendOutcome(bool succeeded, bool hibernate)
        => succeeded ? PowerOutcome.Ok
            : new PowerOutcome(hibernate ? PowerFailure.HibernateUnavailable : PowerFailure.SleepRefused, 0);

    /// <summary>The widget's name, and the label under its icon.</summary>
    public static string Label(PowerAction action) => Label(Strings.Current, action);

    /// <summary><see cref="Label(PowerAction)"/> in an explicit language (the original's own action names).</summary>
    public static string Label(Locale locale, PowerAction action) => action switch
    {
        PowerAction.Shutdown => Strings.Get(locale, Str.Power_Action_Shutdown),
        PowerAction.Restart => Strings.Get(locale, Str.Power_Action_Restart),
        PowerAction.Sleep => Strings.Get(locale, Str.Power_Action_Sleep),
        PowerAction.Hibernate => Strings.Get(locale, Str.Power_Action_Hibernate),
        PowerAction.Lock => Strings.Get(locale, Str.Power_Action_Lock),
        PowerAction.SignOut => Strings.Get(locale, Str.Power_Action_SignOut),
        PowerAction.ScreenOff => Strings.Get(locale, Str.Power_Action_ScreenOff),
        _ => action.ToString(),
    };

    /// <summary>[S-1913] What an armed widget says in place of its name: "Click again to shut down".</summary>
    public static string ArmedLabel(PowerAction action) => ArmedLabel(Strings.Current, action);

    /// <summary><see cref="ArmedLabel(PowerAction)"/> in an explicit language.</summary>
    public static string ArmedLabel(Locale locale, PowerAction action)
        => Strings.Format(locale, Str.WidgetsC_Power_ConfirmHint, ("action", Verb(locale, action)));

    /// <summary>[S-1914] The tooltip of a hibernate widget on a machine that cannot hibernate.</summary>
    public static string UnavailableHint => Str.WidgetsC_Power_HibernateUnavailable.Text();

    /// <summary>
    /// The verb used in sentences: "Could not <i>sleep</i>". Each language words it to fit its own
    /// two sentences (an infinitive in English, a noun in German).
    /// </summary>
    public static string Verb(Locale locale, PowerAction action) => Strings.Get(locale, action switch
    {
        PowerAction.Shutdown => Str.WidgetsC_Power_Verb_Shutdown,
        PowerAction.Restart => Str.WidgetsC_Power_Verb_Restart,
        PowerAction.Sleep => Str.WidgetsC_Power_Verb_Sleep,
        PowerAction.Hibernate => Str.WidgetsC_Power_Verb_Hibernate,
        PowerAction.Lock => Str.WidgetsC_Power_Verb_Lock,
        PowerAction.SignOut => Str.WidgetsC_Power_Verb_SignOut,
        _ => Str.WidgetsC_Power_Verb_ScreenOff,
    });

    /// <summary>
    /// [S-1246] The tile's error line for a failed action: the action and the reason, in words.
    /// Null when it succeeded.
    /// </summary>
    public static string? FailureMessage(PowerAction action, PowerOutcome outcome) => FailureMessage(Strings.Current, action, outcome);

    /// <summary><see cref="FailureMessage(PowerAction, PowerOutcome)"/> in an explicit language.</summary>
    public static string? FailureMessage(Locale locale, PowerAction action, PowerOutcome outcome)
    {
        var reason = outcome.Failure switch
        {
            PowerFailure.None => null,
            PowerFailure.NotAllowed => Strings.Get(locale, Str.WidgetsC_Power_Reason_NotAllowed),
            PowerFailure.HibernateUnavailable => Strings.Get(locale, Str.WidgetsC_Power_Reason_HibernateOff),
            PowerFailure.SleepRefused => Strings.Get(locale, Str.WidgetsC_Power_Reason_SleepRefused),
            _ => outcome.Win32Error switch
            {
                AccessDenied => Strings.Get(locale, Str.WidgetsC_Power_Reason_AccessDenied),
                PrivilegeNotHeld or NotAllAssigned => Strings.Get(locale, Str.WidgetsC_Power_Reason_NotAllowed),
                ShutdownInProgress => Strings.Get(locale, Str.WidgetsC_Power_Reason_ShuttingDown),
                0 => Strings.Get(locale, Str.WidgetsC_Power_Reason_Refused),

                // An error code is an identifier, not a quantity: never digit-grouped.
                var code => Strings.Format(locale, Str.WidgetsC_Power_Reason_WindowsError, ("code", code.ToString(System.Globalization.CultureInfo.InvariantCulture))),
            },
        };

        return reason is null ? null : Strings.Format(locale, Str.WidgetsC_Power_CouldNot, ("action", Verb(locale, action)), ("reason", reason));
    }
}
