using Microsoft.Win32;
using Themia.Core.Diagnostics;
using Themia.Core.Shell;

namespace Themia.App.Services;

/// <summary>
/// [S-933] "Start with Windows", for both ways Themia is installed.
/// <para>
/// The registration is picked once, by package identity, and the rest of the app never knows which
/// one it got: unpackaged builds write a per-user <c>Run</c> value (no elevation, nothing
/// machine-wide); the MSIX build asks for its manifest's <c>StartupTask</c>, the only startup entry a
/// packaged app may own. Shipping the package later changes nothing here — the same switch, the
/// same states, the same advice.
/// </para>
/// <para>
/// Windows owns the answer either way: the user can switch Themia off as a startup app in Settings or
/// Task Manager, and a policy can decide for them. So the settings switch shows what Windows will
/// actually do at the next sign-in ([S-936]), and a change that does not take says what to do about
/// it ([S-935]) instead of snapping back silently.
/// </para>
/// </summary>
internal static class Autostart
{
    private const string Scope = "startup";

    /// <summary>Serialises changes, so two quick flips cannot interleave a read-modify-write.</summary>
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private static readonly IRegistration Registration =
        AutostartRules.MechanismFor(PackageIdentity.IsPackaged) == AutostartMechanism.StartupTask
            ? new StartupTaskRegistration()
            : new RunKeyRegistration();

    /// <summary>How this process was started. Set once by <see cref="DetectLaunch"/> before anything else runs.</summary>
    public static LaunchSource LaunchSource { get; private set; } = LaunchSource.Manual;

    /// <summary>
    /// [S-934] Read how this process was started: the <c>--autostart</c> argument the registration
    /// launches with, or a packaged startup-task activation. Before the log files are installed: the
    /// caller logs it once they are.
    /// </summary>
    public static void DetectLaunch(IReadOnlyList<string> arguments)
    {
        LaunchSource = AutostartRules.SourceOf(arguments, PackageIdentity.IsPackaged && StartupTaskRegistration.WasActivatedByTask());
    }

    /// <summary>What Windows will do at the next sign-in.</summary>
    public static async Task<AutostartStatus> ReadAsync()
    {
        try
        {
            return new AutostartStatus(await Registration.ReadAsync().ConfigureAwait(true));
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "could not read the startup registration", ex);
            return new AutostartStatus(AutostartState.Unknown, AutostartProblem.None, ex.Message);
        }
    }

    /// <summary>Ask for Themia to start (or not) at sign-in. Returns what was actually reached, and why when it is not what was asked.</summary>
    public static async Task<AutostartStatus> SetAsync(bool enabled)
    {
        await Gate.WaitAsync().ConfigureAwait(true);
        try
        {
            var reached = await Registration.SetAsync(enabled).ConfigureAwait(true);
            var outcome = AutostartRules.Outcome(enabled, reached);
            if (outcome.Problem != AutostartProblem.None)
            {
                Log.Warn(Scope, $"asked to {(enabled ? "start" : "not start")} with Windows; Windows reports {reached}");
            }

            return outcome;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            Log.Error(Scope, $"Windows refused to {(enabled ? "add" : "remove")} the startup entry", ex);
            return new AutostartStatus(await SafeReadState().ConfigureAwait(true), AutostartProblem.AccessDenied, ex.Message);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"could not {(enabled ? "add" : "remove")} the startup entry", ex);
            return new AutostartStatus(await SafeReadState().ConfigureAwait(true), AutostartProblem.Failed, ex.Message);
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task<AutostartState> SafeReadState()
    {
        try
        {
            return await Registration.ReadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "could not re-read the startup registration after a failed change", ex);
            return AutostartState.Unknown;
        }
    }

    private interface IRegistration
    {
        Task<AutostartState> ReadAsync();

        /// <summary>Make the change and return the state Windows reports afterwards. Throws on a refused write.</summary>
        Task<AutostartState> SetAsync(bool enabled);
    }

    /// <summary>
    /// [S-936] The unpackaged registration: <c>HKCU\...\Run\Themia</c> = <c>"…\Themia.exe" --autostart</c>,
    /// and Windows' own startup-app switch for it under <c>Explorer\StartupApproved\Run</c>.
    /// </summary>
    private sealed class RunKeyRegistration : IRegistration
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

        public Task<AutostartState> ReadAsync() => Task.FromResult(Read());

        public Task<AutostartState> SetAsync(bool enabled)
        {
            using (var run = Registry.CurrentUser.CreateSubKey(RunKey, writable: true))
            {
                if (enabled)
                {
                    run.SetValue(AutostartRules.RunValueName, AutostartRules.RunCommand(ExecutablePath()), RegistryValueKind.String);
                }
                else
                {
                    run.DeleteValue(AutostartRules.RunValueName, throwOnMissingValue: false);
                }
            }

            // The user has just asked for it here, which is newer than a "disabled" they once set in
            // Task Manager. Removing Windows' record puts the entry back to its default, allowed —
            // the one thing a packaged app is not permitted to do, and an unpackaged one may.
            using (var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: true))
            {
                approved?.DeleteValue(AutostartRules.RunValueName, throwOnMissingValue: false);
            }

            return Task.FromResult(Read());
        }

        private static AutostartState Read()
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            var launchesThis = AutostartRules.LaunchesThis(run?.GetValue(AutostartRules.RunValueName) as string, ExecutablePath());

            using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: false);
            var allowed = AutostartRules.StartupApproved(approved?.GetValue(AutostartRules.RunValueName) as byte[]);

            return AutostartRules.FromRunKey(launchesThis, allowed);
        }

        private static string ExecutablePath()
            => Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Themia.exe");
    }

    /// <summary>
    /// [S-937] The packaged registration: the manifest's <c>windows.startupTask</c> extension with id
    /// <see cref="AutostartRules.StartupTaskId"/>. Windows only lets the app turn it on from "off"; once
    /// the user has switched it off in Windows, or a policy decides, the app can only say so.
    /// </summary>
    private sealed class StartupTaskRegistration : IRegistration
    {
        public async Task<AutostartState> ReadAsync()
        {
            var task = await Windows.ApplicationModel.StartupTask.GetAsync(AutostartRules.StartupTaskId);
            return AutostartRules.FromStartupTask((int)task.State);
        }

        public async Task<AutostartState> SetAsync(bool enabled)
        {
            var task = await Windows.ApplicationModel.StartupTask.GetAsync(AutostartRules.StartupTaskId);
            var state = AutostartRules.FromStartupTask((int)task.State);

            if (enabled && state == AutostartState.Off)
            {
                return AutostartRules.FromStartupTask((int)await task.RequestEnableAsync());
            }

            if (!enabled && state == AutostartState.On)
            {
                task.Disable();
                return AutostartRules.FromStartupTask((int)task.State);
            }

            return state;
        }

        /// <summary>Whether Windows started this process through the startup task. Only meaningful with package identity.</summary>
        public static bool WasActivatedByTask()
        {
            try
            {
                return Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs().Kind
                    == Microsoft.Windows.AppLifecycle.ExtendedActivationKind.StartupTask;
            }
            catch (Exception ex)
            {
                Log.Warn(Scope, "could not read how Windows activated the app; treating it as started by hand", ex);
                return false;
            }
        }
    }
}
