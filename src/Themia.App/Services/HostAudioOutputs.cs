using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Services;

/// <summary>[S-6913] The output devices a widget asks for (<see cref="IWidgetHost.Audio"/>): the app's one <see cref="CoreAudio"/> worker.</summary>
internal sealed class HostAudioOutputs : IAudioOutputs
{
    public static HostAudioOutputs Instance { get; } = new();

    private HostAudioOutputs()
    {
    }

    public IDefaultOutputSwitch DefaultOutput => CoreAudio.DefaultOutputSwitch.Instance;

    public IAudioSubscription Subscribe(AudioWant want, Action<AudioSnapshot> onSnapshot) => CoreAudio.Subscribe(want, onSnapshot);

    public void SetLevel(string? deviceId, string key, double level) => CoreAudio.SetLevel(deviceId, key, level);

    public void SetMute(string? deviceId, string key, bool muted) => CoreAudio.SetMute(deviceId, key, muted);
}
