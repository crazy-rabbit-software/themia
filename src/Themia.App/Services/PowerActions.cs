using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Themia.Core.Diagnostics;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>
/// [S-1251] A preview card's power backend: logs the action it was asked for and does nothing. It
/// answers the capability probe truthfully, so a preview shows what this machine can do.
/// </summary>
internal sealed class InertPowerActions : IPowerBackend
{
    public static InertPowerActions Instance { get; } = new();

    public bool CanHibernate => Win32PowerActions.Instance.CanHibernate;

    public PowerOutcome Run(PowerAction action)
    {
        Log.Info("power", $"preview: {action} was pressed and not run");
        return PowerOutcome.Ok;
    }
}

/// <summary>
/// [S-1243] The real power backend: Win32 calls only, no <c>shutdown.exe</c> spawn — the CLI hop
/// costs a process, loses the error, and cannot express "sleep".
/// <para>
/// What each action calls is decided by <see cref="PowerActionRules.Plan"/> (tested); this file
/// executes that plan. The process token is a <see cref="SafeHandle"/>, closed on every path.
/// </para>
/// </summary>
internal sealed partial class Win32PowerActions : IPowerBackend
{
    private const string Scope = "power";

    private const uint TokenAdjustPrivileges = 0x0020;
    private const uint TokenQuery = 0x0008;
    private const uint PrivilegeEnabled = 0x00000002;

    private const nint HwndBroadcast = 0xFFFF;
    private const uint WmSysCommand = 0x0112;
    private const nuint ScMonitorPower = 0xF170;

    /// <summary><c>SC_MONITORPOWER</c>'s lParam: 2 = off (1 = low power, -1 = on).</summary>
    private const nint MonitorOff = 2;

    private const uint SmtoAbortIfHung = 0x0002;

    public static Win32PowerActions Instance { get; } = new();

    /// <summary>
    /// [S-1914] <c>IsPwrHibernateAllowed</c>: false when hibernation is off (<c>powercfg /h off</c>,
    /// no hibernation file) or the hardware or policy does not allow it. Read-only and cheap.
    /// </summary>
    public bool CanHibernate
    {
        get
        {
            try
            {
                return IsPwrHibernateAllowed() != 0;
            }
            catch (Exception ex)
            {
                // A missing export would be a broken Windows; say so and offer nothing that cannot run.
                Log.Warn(Scope, "IsPwrHibernateAllowed failed", ex);
                return false;
            }
        }
    }

    public PowerOutcome Run(PowerAction action)
    {
        try
        {
            var call = PowerActionRules.Plan(action);

            if (call.NeedsShutdownPrivilege)
            {
                var privilege = EnableShutdownPrivilege();

                if (!privilege.Succeeded)
                {
                    return privilege;
                }
            }

            return call.Kind switch
            {
                PowerCallKind.ExitWindows => ExitWindowsEx(call.ExitFlags, PowerActionRules.PlannedReason)
                    ? PowerOutcome.Ok
                    : PowerOutcome.Error(Marshal.GetLastPInvokeError()),

                // force = false so drivers may veto (the documented value since Vista); wake events
                // stay enabled so a scheduled task or Wake-on-LAN still brings the machine back.
                // Returns only after the machine RESUMES: this owns its worker thread for the sleep.
                PowerCallKind.Suspend => PowerActionRules.SuspendOutcome(
                    SetSuspendState(call.Hibernate ? (byte)1 : (byte)0, 0, 0) != 0, call.Hibernate),

                PowerCallKind.Lock => LockWorkStation()
                    ? PowerOutcome.Ok
                    : PowerOutcome.Error(Marshal.GetLastPInvokeError()),

                PowerCallKind.MonitorOff => ScreenOff(),
                _ => PowerOutcome.Error(0),
            };
        }
        catch (Exception ex)
        {
            // Never let a failure escape a worker thread unobserved: the widget shows the error.
            Log.Error(Scope, $"{action} threw", ex);
            return PowerOutcome.Error(0);
        }
    }

    /// <summary>
    /// <c>SeShutdownPrivilege</c> is present but disabled in an interactive user's token. Enabled per
    /// call rather than cached: it is cheap, and a cached "enabled" would not survive a token change.
    /// </summary>
    private static unsafe PowerOutcome EnableShutdownPrivilege()
    {
        if (!OpenProcessToken(GetCurrentProcess(), TokenAdjustPrivileges | TokenQuery, out var token))
        {
            return PowerOutcome.Error(Marshal.GetLastPInvokeError());
        }

        using (token)
        {
            if (!LookupPrivilegeValue(null, "SeShutdownPrivilege", out var luid))
            {
                return PowerOutcome.Error(Marshal.GetLastPInvokeError());
            }

            var privileges = new TokenPrivileges
            {
                PrivilegeCount = 1,
                Luid = luid,
                Attributes = PrivilegeEnabled,
            };

            var adjusted = AdjustTokenPrivileges(token, false, &privileges, 0, null, null);

            // Success is not enough: ERROR_NOT_ALL_ASSIGNED is the only sign it assigned nothing.
            return PowerActionRules.PrivilegeOutcome(adjusted, Marshal.GetLastPInvokeError());
        }
    }

    /// <summary>
    /// The monitor-off broadcast. <c>SendMessageTimeout</c> rather than <c>SendMessage</c>: a broadcast
    /// waits on every top-level window, so one wedged app would otherwise hang this thread. Its
    /// aggregate return means nothing (zero is what a window that ignored the message returns).
    /// </summary>
    private static PowerOutcome ScreenOff()
    {
        _ = SendMessageTimeout(HwndBroadcast, WmSysCommand, ScMonitorPower, MonitorOff, SmtoAbortIfHung, 1000, out _);
        return PowerOutcome.Ok;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    /// <summary><c>TOKEN_PRIVILEGES</c> with its one-element array inlined.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct TokenPrivileges
    {
        public uint PrivilegeCount;
        public Luid Luid;
        public uint Attributes;
    }

    /// <summary>The process token, closed exactly once on every path.</summary>
    private sealed class TokenHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public TokenHandle()
            : base(ownsHandle: true)
        {
        }

        protected override bool ReleaseHandle() => CloseHandle(handle);
    }

    [LibraryImport("kernel32.dll")]
    private static partial nint GetCurrentProcess();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenProcessToken(nint process, uint access, out TokenHandle token);

    [LibraryImport("advapi32.dll", EntryPoint = "LookupPrivilegeValueW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool LookupPrivilegeValue(string? system, string name, out Luid luid);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool AdjustTokenPrivileges(
        TokenHandle token,
        [MarshalAs(UnmanagedType.Bool)] bool disableAll,
        TokenPrivileges* newState,
        uint bufferLength,
        TokenPrivileges* previousState,
        uint* returnLength);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ExitWindowsEx(uint flags, uint reason);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool LockWorkStation();

    [LibraryImport("powrprof.dll")]
    private static partial byte IsPwrHibernateAllowed();

    [LibraryImport("powrprof.dll")]
    private static partial byte SetSuspendState(byte hibernate, byte force, byte wakeupEventsDisabled);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageTimeoutW")]
    private static partial nint SendMessageTimeout(nint window, uint message, nuint wParam, nint lParam, uint flags, uint timeout, out nuint result);
}
