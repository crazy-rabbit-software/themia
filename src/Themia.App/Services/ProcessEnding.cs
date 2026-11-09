using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.App.Services;

/// <summary>
/// [S-2437] The host's half of End task (<see cref="ProcessControl"/>): open the process for
/// terminating, check that the pid still names the process the list read (its start time; Windows
/// hands a pid on within seconds), then end it as Task Manager's End task does. Themia runs
/// unelevated, so a protected, elevated or system process refuses the handle: that is
/// <see cref="ProcessEndResult.Denied"/>, never retried with more rights.
/// </summary>
internal static partial class ProcessEnding
{
    private const string Scope = "process-end";

    private const uint Terminate = 0x0001;
    private const uint QueryLimitedInformation = 0x1000;
    private const int ErrorAccessDenied = 5;
    private const int ErrorInvalidParameter = 87;

    public static ProcessEndResult End(ProcessIdentity process)
    {
        if (process.Pid <= 0)
        {
            return ProcessEndResult.Gone;
        }

        using var handle = OpenProcess(Terminate | QueryLimitedInformation, false, (uint)process.Pid);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastPInvokeError();

            // No such pid is a process that exited since the read; anything else is a refusal.
            if (error == ErrorInvalidParameter)
            {
                return ProcessEndResult.Gone;
            }

            Log.Info(Scope, $"Windows would not open process {process.Pid} to end it (error {error})");
            return ProcessEndResult.Denied;
        }

        if (!GetProcessTimes(handle, out var created, out _, out _, out _))
        {
            Log.Warn(Scope, $"could not read when process {process.Pid} started (error {Marshal.GetLastPInvokeError()}); not ending it");
            return ProcessEndResult.Denied;
        }

        if (created != process.CreateTime)
        {
            Log.Info(Scope, $"pid {process.Pid} is another process now; not ending it");
            return ProcessEndResult.Gone;
        }

        if (!TerminateProcess(handle, 1))
        {
            var error = Marshal.GetLastPInvokeError();
            Log.Info(Scope, $"Windows would not end process {process.Pid} (error {error})");
            return error == ErrorAccessDenied ? ProcessEndResult.Denied : ProcessEndResult.Gone;
        }

        Log.Info(Scope, $"ended process {process.Pid}");
        return ProcessEndResult.Ended;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint pid);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetProcessTimes(SafeProcessHandle process, out long creation, out long exit, out long kernel, out long user);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool TerminateProcess(SafeProcessHandle process, uint exitCode);
}
