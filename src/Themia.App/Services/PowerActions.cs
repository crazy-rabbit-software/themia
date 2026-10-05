using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Themia.Core.Diagnostics;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>
/// [S-1243] The seam between the power tile and the machine. The tile decides <i>whether</i> to run
/// an action (<see cref="PowerArm"/>); this only runs it. Two implementations: the real one, and an
/// inert one preview cards get ([S-1251]), so a widget drawn as a preview can never shut the
/// PC down.
/// </summary>
internal interface IPowerActions
{
    /// <summary>
    /// Run <paramref name="action"/> and say how it went. Blocking — sleep blocks until the machine
    /// resumes — so callers run it on a worker thread, never the UI thread. Never throws.
    /// </summary>
    PowerOutcome Run(PowerAction action);
}

/// <summary>[S-1251] A preview card's power backend: logs the action it was asked for and does nothing.</summary>
internal sealed class InertPowerActions : IPowerActions
{
    public static InertPowerActions Instance { get; } = new();

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
internal sealed partial class Win32PowerActions : IPowerActions
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
            // Never let a failure escape a worker thread unobserved: the tile shows the error.
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
    private static partial byte SetSuspendState(byte hibernate, byte force, byte wakeupEventsDisabled);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageTimeoutW")]
    private static partial nint SendMessageTimeout(nint window, uint message, nuint wParam, nint lParam, uint flags, uint timeout, out nuint result);
}
