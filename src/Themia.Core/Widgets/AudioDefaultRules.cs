using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>[S-1615] The roles a default audio endpoint is set for (Core Audio's <c>ERole</c>, same values).</summary>
public enum AudioRole
{
    /// <summary>Games, system sounds, the volume keys and flyout.</summary>
    Console = 0,

    /// <summary>Music and video.</summary>
    Multimedia = 1,

    /// <summary>Calls.</summary>
    Communications = 2,
}

/// <summary>[S-1616] How asking Windows to change its default output went.</summary>
public enum DefaultSwitchResult
{
    /// <summary>The device is now Windows' default output.</summary>
    Switched = 0,

    /// <summary>This Windows build has no <c>IPolicyConfig</c> (the class or the interface is missing).</summary>
    NotSupported = 1,

    /// <summary>Windows refused (policy, a locked-down account).</summary>
    AccessDenied = 2,

    /// <summary>The device went away between the menu opening and the pick.</summary>
    DeviceGone = 3,

    /// <summary>Any other failure, or no audio worker to ask.</summary>
    Failed = 4,
}

/// <summary>
/// [S-1617] The seam in front of the one call that changes the machine's default output. The app's
/// implementation talks to Core Audio on its worker thread; tests pass a fake, so nothing under test
/// can ever switch a real device.
/// </summary>
public interface IDefaultOutputSwitch
{
    /// <summary>Make <paramref name="deviceId"/> Windows' default output for every role. Never throws.</summary>
    Task<DefaultSwitchResult> SetDefaultOutputAsync(string deviceId);
}

/// <summary>What a pick in an audio widget's device picker asks for.</summary>
public enum DevicePickKind
{
    /// <summary>Nothing changes (a preview, or the pick is what the widget already shows).</summary>
    Nothing = 0,

    /// <summary>Drop the widget's own device and follow Windows' default again; Windows is not touched.</summary>
    Follow = 1,

    /// <summary>Make the picked device Windows' default output.</summary>
    SwitchDefault = 2,
}

/// <summary>[S-1614] A pick, decided.</summary>
public readonly record struct DevicePickPlan(DevicePickKind Kind, AudioDevice? Device = null);

/// <summary>[S-1616] What the widget's config holds after a pick, and what it tells the user (null: nothing).</summary>
public sealed record DevicePickResult(string? DeviceId, string? DeviceName, string? Notice);

/// <summary>
/// [S-1614]..[S-1617] The audio widgets' device picker: picking a device makes it Windows' default
/// output (console, multimedia and communications), and the widget goes on following the default.
/// When Windows will not switch, the widget binds to the device instead ([S-1221]) and says why.
/// <para>
/// The switch itself is <c>IPolicyConfig::SetDefaultEndpoint</c>, the undocumented interface Sound
/// settings itself uses. Everything around it — when to call it, what its HRESULTs mean, what the
/// widget does afterwards — is here, where it can be tested without an audio device.
/// </para>
/// </summary>
public static class AudioDefaultRules
{
    private const int ClassNotRegistered = unchecked((int)0x80040154); // REGDB_E_CLASSNOTREG
    private const int ClassNotAvailable = unchecked((int)0x80040111); // CLASS_E_CLASSNOTAVAILABLE
    private const int NoInterface = unchecked((int)0x80004002); // E_NOINTERFACE
    private const int NotImplemented = unchecked((int)0x80004001); // E_NOTIMPL
    private const int AccessDenied = unchecked((int)0x80070005); // E_ACCESSDENIED
    private const int NotFound = unchecked((int)0x80070490); // E_NOTFOUND
    private const int InvalidArgument = unchecked((int)0x80070057); // E_INVALIDARG: an id Windows does not know
    private const int DeviceInvalidated = unchecked((int)0x88890004); // AUDCLNT_E_DEVICE_INVALIDATED

    /// <summary>[S-1615] Every role, console first: the console role decides whether the switch happened.</summary>
    public static IReadOnlyList<AudioRole> Roles { get; } = [AudioRole.Console, AudioRole.Multimedia, AudioRole.Communications];

    /// <summary>
    /// [S-1614] What a pick means. A preview never changes anything ([S-1233]). "Follow the default"
    /// and picking the device that already is the default both mean following it — Windows is not
    /// asked to set what it already has. Any other device is made the default.
    /// </summary>
    /// <param name="preview">The widget is a preview card (a debug render).</param>
    /// <param name="picked">The picked device, or null for "Follow the default output".</param>
    /// <param name="boundId">The widget's own device id, or null when it follows the default.</param>
    /// <param name="defaultId">Windows' default output now, or null when there is none.</param>
    public static DevicePickPlan Plan(bool preview, AudioDevice? picked, string? boundId, string? defaultId)
    {
        if (preview)
        {
            return new DevicePickPlan(DevicePickKind.Nothing);
        }

        var following = string.IsNullOrEmpty(boundId);

        if (picked is null || string.Equals(picked.Id, defaultId, StringComparison.OrdinalIgnoreCase))
        {
            return following ? new DevicePickPlan(DevicePickKind.Nothing) : new DevicePickPlan(DevicePickKind.Follow);
        }

        return new DevicePickPlan(DevicePickKind.SwitchDefault, picked);
    }

    /// <summary>[S-1616] What one failed (or successful) COM call means.</summary>
    public static DefaultSwitchResult Classify(int hresult) => hresult switch
    {
        >= 0 => DefaultSwitchResult.Switched,
        ClassNotRegistered or ClassNotAvailable or NoInterface or NotImplemented => DefaultSwitchResult.NotSupported,
        AccessDenied => DefaultSwitchResult.AccessDenied,
        NotFound or InvalidArgument or DeviceInvalidated => DefaultSwitchResult.DeviceGone,
        _ => DefaultSwitchResult.Failed,
    };

    /// <summary>
    /// [S-1615] Whether to go on to the next role after <paramref name="role"/> answered
    /// <paramref name="hresult"/>: a console role Windows refused stops the switch (nothing changed),
    /// while a later role that fails is logged and the switch still counts — the console default is
    /// what the volume keys, the flyout and the widget follow.
    /// </summary>
    public static bool Continue(AudioRole role, int hresult) => role != AudioRole.Console || hresult >= 0;

    /// <summary>[S-1615] The outcome of the per-role results, in <see cref="Roles"/> order.</summary>
    public static DefaultSwitchResult Outcome(IReadOnlyList<int> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return results.Count == 0 ? DefaultSwitchResult.Failed : Classify(results[0]);
    }

    /// <summary>
    /// [S-1616] The widget after a switch attempt. Switched: it follows the default again (which is now
    /// the picked device). Refused: it binds to the picked device, so the pick still does what the user
    /// can see, and says why. A device that went away leaves the widget as it was.
    /// </summary>
    public static DevicePickResult After(DefaultSwitchResult result, AudioDevice device, string? boundId, string? boundName, Locale? locale = null)
    {
        ArgumentNullException.ThrowIfNull(device);
        var language = locale ?? Strings.Current;
        var named = !string.IsNullOrWhiteSpace(device.Name);
        var name = named ? device.Name.Trim() : Strings.Get(language, Str.WidgetsA_Audio_TheDevice);

        return result switch
        {
            DefaultSwitchResult.Switched => new DevicePickResult(null, null, null),
            DefaultSwitchResult.DeviceGone => new DevicePickResult(boundId, boundName, named
                ? Strings.Format(language, Str.WidgetsA_Audio_DeviceGone, ("name", name))
                : Strings.Get(language, Str.WidgetsA_Audio_DeviceGoneUnnamed)),
            DefaultSwitchResult.NotSupported => Bound(device, Strings.Format(language, Str.WidgetsA_Audio_SwitchNotSupported, ("name", name))),
            DefaultSwitchResult.AccessDenied => Bound(device, Strings.Format(language, Str.WidgetsA_Audio_SwitchDenied, ("name", name))),
            _ => Bound(device, Strings.Format(language, Str.WidgetsA_Audio_SwitchFailed, ("name", name))),
        };
    }

    /// <summary>
    /// [S-1614]..[S-1617] A pick, start to end: plan it, ask Windows through <paramref name="output"/>
    /// when the plan says so, and return what the widget becomes — or null when nothing changes.
    /// </summary>
    public static async Task<DevicePickResult?> PickAsync(
        bool preview,
        AudioDevice? picked,
        string? boundId,
        string? boundName,
        string? defaultId,
        IDefaultOutputSwitch output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var plan = Plan(preview, picked, boundId, defaultId);

        switch (plan.Kind)
        {
            case DevicePickKind.Follow:
                return new DevicePickResult(null, null, null);

            case DevicePickKind.SwitchDefault when plan.Device is { } device:
                var result = await output.SetDefaultOutputAsync(device.Id).ConfigureAwait(true);
                return After(result, device, boundId, boundName);

            default:
                return null;
        }
    }

    private static DevicePickResult Bound(AudioDevice device, string notice)
        => new(device.Id, string.IsNullOrWhiteSpace(device.Name) ? null : device.Name.Trim(), notice);
}
