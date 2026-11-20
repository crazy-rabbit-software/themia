using System.Collections.Concurrent;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;
using Themia.App.Interop;

namespace Themia.App.Services;

/// <summary>
/// [S-1615] Changing Windows' default output device, for the audio widgets' device pickers.
/// <para>
/// Windows has no documented API for this. Sound settings, the volume flyout and every audio
/// switcher use <c>IPolicyConfig</c> on the <c>PolicyConfigClient</c> class, an interface that has
/// kept its id and layout since Windows 7; Themia calls its <c>SetDefaultEndpoint</c> for the
/// console, multimedia and communications roles, console first. Because it is undocumented, a
/// Windows build may lack it or refuse it: every failure comes back as a
/// <see cref="DefaultSwitchResult"/>, and the widget binds to the device instead
/// (<see cref="AudioDefaultRules.After"/>).
/// </para>
/// <para>
/// The call runs on the Core Audio worker, like every other Core Audio call: it blocks, and it
/// belongs in the multithreaded apartment. The default-changed notification it causes wakes the
/// worker, which rebinds every widget that follows the default — nothing here waits for it.
/// </para>
/// </summary>
internal static unsafe partial class CoreAudio
{
    private static class PolicyConfig
    {
        /// <summary>CLSID_PolicyConfigClient.</summary>
        public static readonly Guid Class = new("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9");

        /// <summary>IID_IPolicyConfig (Windows 7 and later).</summary>
        public static readonly Guid Interface = new("F8679F50-850A-41CF-9C72-430F290290C8");

        /// <summary>
        /// IUnknown's three, then GetMixFormat, GetDeviceFormat, ResetDeviceFormat, SetDeviceFormat,
        /// GetProcessingPeriod, SetProcessingPeriod, GetShareMode, SetShareMode, GetPropertyValue,
        /// SetPropertyValue — then SetDefaultEndpoint(LPCWSTR id, ERole role).
        /// </summary>
        public const int SetDefaultEndpoint = 13;
    }

    /// <summary>
    /// [S-1615] Make a device Windows' default output for every role. Answered on a pool thread once
    /// the worker has tried; never throws. Without a running worker (no audio widget on the desktop)
    /// it fails rather than starting one for a single call.
    /// </summary>
    public static Task<DefaultSwitchResult> SetDefaultOutputAsync(string deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            return Task.FromResult(DefaultSwitchResult.DeviceGone);
        }

        if (_worker is not { } worker)
        {
            Log.Warn(Scope, "asked to change the default output with no audio widget running");
            return Task.FromResult(DefaultSwitchResult.Failed);
        }

        return worker.PostDefault(deviceId);
    }

    /// <summary>[S-1617] The app's side of the seam the device pickers go through.</summary>
    internal sealed class DefaultOutputSwitch : IDefaultOutputSwitch
    {
        public static DefaultOutputSwitch Instance { get; } = new();

        private DefaultOutputSwitch()
        {
        }

        public Task<DefaultSwitchResult> SetDefaultOutputAsync(string deviceId) => CoreAudio.SetDefaultOutputAsync(deviceId);
    }

    private sealed partial class Worker
    {
        private readonly ConcurrentQueue<(string Id, TaskCompletionSource<DefaultSwitchResult> Done)> _defaults = new();

        public Task<DefaultSwitchResult> PostDefault(string id)
        {
            var done = new TaskCompletionSource<DefaultSwitchResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _defaults.Enqueue((id, done));

            if (_stopping)
            {
                // The worker may already be past its last drain: answer here so nothing waits forever.
                FailDefaultRequests();
            }
            else
            {
                Wake();
            }

            return done.Task;
        }

        /// <summary>Worker thread: try every waiting pick, oldest first.</summary>
        private void ApplyDefaultRequests()
        {
            while (_defaults.TryDequeue(out var request))
            {
                DefaultSwitchResult result;

                try
                {
                    result = SwitchDefault(request.Id);
                }
                catch (Exception ex)
                {
                    Log.Error(Scope, "changing the default output failed", ex);
                    result = DefaultSwitchResult.Failed;
                }

                request.Done.TrySetResult(result);
            }
        }

        private void FailDefaultRequests()
        {
            while (_defaults.TryDequeue(out var request))
            {
                request.Done.TrySetResult(DefaultSwitchResult.Failed);
            }
        }
    }

    /// <summary>
    /// [S-1615] One <c>PolicyConfigClient</c>, created for the call and released on every path: the
    /// device made the default for each role in <see cref="AudioDefaultRules.Roles"/>, stopping when
    /// the console role is refused.
    /// </summary>
    private static DefaultSwitchResult SwitchDefault(string id)
    {
        var created = ComApi.Create(PolicyConfig.Class, PolicyConfig.Interface, ComApi.ClsCtxInprocServer, out var policy);
        if (created < 0 || policy == 0)
        {
            Log.Warn(Scope, $"Windows' policy config is not available, so the default output cannot be changed: 0x{created:X8}");
            return created < 0 ? AudioDefaultRules.Classify(created) : DefaultSwitchResult.NotSupported;
        }

        try
        {
            var results = new List<int>(AudioDefaultRules.Roles.Count);

            fixed (char* text = id)
            {
                foreach (var role in AudioDefaultRules.Roles)
                {
                    var result = ((delegate* unmanaged[Stdcall]<nint, char*, int, int>)ComApi.Slot(policy, PolicyConfig.SetDefaultEndpoint))(policy, text, (int)role);
                    results.Add(result);

                    if (result < 0)
                    {
                        Log.Warn(Scope, $"could not make the device the default for the {role} role: 0x{result:X8}");
                    }

                    if (!AudioDefaultRules.Continue(role, result))
                    {
                        break;
                    }
                }
            }

            var outcome = AudioDefaultRules.Outcome(results);

            if (outcome == DefaultSwitchResult.Switched)
            {
                Log.Info(Scope, "changed Windows' default output device");
            }

            return outcome;
        }
        finally
        {
            _ = ComApi.Release(policy);
        }
    }
}
