using Themia.Core.Diagnostics;
using Themia.Core.Widgets;
using Windows.Foundation;
using Windows.Media.Audio;
using Windows.Media.Devices;

namespace Themia.App.Services.Toggles;

/// <summary>
/// [S-3914] Spatial sound on the default output, as Karpo's spatial toggle has it (<c>Spatial.cs</c>):
/// what is on is documented — <c>SpatialAudioDeviceConfiguration</c> for the output's interface path
/// (<c>MediaDevice.GetDefaultAudioRenderId</c>), whose active format is a GUID and none (GUID_NULL) is off,
/// followed through its <c>ConfigurationChanged</c> and <c>MediaDevice.DefaultAudioRenderDeviceChanged</c>.
/// A click turns it off, or on as Windows Sonic for Headphones, which ships with Windows
/// (<c>SetDefaultSpatialAudioFormatAsync</c>). Windows keeps that choice for Settings and may refuse an
/// app; the click then says so. Karpo's list of installed formats (Dolby, DTS) and its "when on" setting
/// are not ported: the toggle has no settings page.
/// </summary>
internal sealed class SpatialToggleSource : ToggleSource
{
    private readonly Lock _gate = new();
    private SpatialAudioDeviceConfiguration? _configuration;
    private TypedEventHandler<SpatialAudioDeviceConfiguration, object>? _configurationChanged;
    private TypedEventHandler<object, DefaultAudioRenderDeviceChangedEventArgs>? _defaultChanged;

    public override void Start() => OffThread("following spatial sound", () =>
    {
        TypedEventHandler<object, DefaultAudioRenderDeviceChangedEventArgs> handler = (_, args) =>
        {
            if (args.Role == AudioDeviceRole.Default)
            {
                OffThread("following a new output", Bind);
            }
        };

        try
        {
            MediaDevice.DefaultAudioRenderDeviceChanged += handler;
            lock (_gate)
            {
                _defaultChanged = handler;
            }
        }
        catch (Exception ex)
        {
            // A new default output is then picked up when the pointer next comes by.
            Log.Warn(Scope, $"the default output cannot be followed: {ex.Message}");
        }

        Bind();
    });

    public override void Refresh() => OffThread("reading spatial sound", Bind);

    /// <summary>The default output's configuration, followed, and read.</summary>
    private void Bind()
    {
        if (Disposed)
        {
            return;
        }

        SpatialAudioDeviceConfiguration? next = null;
        try
        {
            var id = MediaDevice.GetDefaultAudioRenderId(AudioDeviceRole.Default);
            if (!string.IsNullOrEmpty(id))
            {
                next = SpatialAudioDeviceConfiguration.GetForDeviceId(id);
            }
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, $"the output's spatial sound could not be read: {ex.Message}");
        }

        SpatialAudioDeviceConfiguration? before;
        TypedEventHandler<SpatialAudioDeviceConfiguration, object>? handler;
        lock (_gate)
        {
            if (Disposed)
            {
                return;
            }

            before = _configuration;
            _configuration = next;
            handler = _configurationChanged ??= (_, _) => OffThread("reading spatial sound", Read);
        }

        if (!ReferenceEquals(before, next))
        {
            Unfollow(before, handler);
            if (next is not null)
            {
                try
                {
                    next.ConfigurationChanged += handler;
                }
                catch (Exception ex)
                {
                    Log.Warn(Scope, $"the output's spatial sound cannot be followed: {ex.Message}");
                }
            }
        }

        Read();
    }

    private void Read()
    {
        SpatialAudioDeviceConfiguration? configuration;
        lock (_gate)
        {
            configuration = _configuration;
        }

        if (Disposed)
        {
            return;
        }

        if (configuration is null)
        {
            Publish(ToggleReading.Blocked(ToggleBlock.NoDevice));
            return;
        }

        try
        {
            if (!configuration.IsSpatialAudioSupported || !configuration.IsSpatialAudioFormatSupported(SpatialAudioFormatSubtype.WindowsSonic))
            {
                Publish(ToggleReading.Blocked(ToggleBlock.NotSupported));
                return;
            }

            Publish(ToggleReading.Of(!QuickToggleRules.SpatialIsOff(configuration.ActiveSpatialAudioFormat)));
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, $"the output's spatial sound could not be read: {ex.Message}");
            Publish(ToggleReading.Pending);
        }
    }

    public override Task<bool> SetAsync(bool on) => Task.Run(async () =>
    {
        SpatialAudioDeviceConfiguration? configuration;
        lock (_gate)
        {
            configuration = _configuration;
        }

        if (configuration is null)
        {
            return false;
        }

        try
        {
            var format = on ? SpatialAudioFormatSubtype.WindowsSonic : QuickToggleRules.SpatialOff;
            var result = await configuration.SetDefaultSpatialAudioFormatAsync(format);
            if (result.Status != SetDefaultSpatialAudioFormatStatus.Succeeded)
            {
                Log.Warn(Scope, $"switching spatial sound answered {result.Status}");
            }

            Read();
            return result.Status == SetDefaultSpatialAudioFormatStatus.Succeeded;
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "spatial sound could not be switched", ex);
            return false;
        }
    });

    private void Unfollow(SpatialAudioDeviceConfiguration? configuration, TypedEventHandler<SpatialAudioDeviceConfiguration, object>? handler)
    {
        if (configuration is null || handler is null)
        {
            return;
        }

        try
        {
            configuration.ConfigurationChanged -= handler;
        }
        catch (Exception ex)
        {
            Log.Debug(Scope, $"an output's spatial sound handler could not be removed: {ex.Message}");
        }
    }

    protected override void Stop()
    {
        SpatialAudioDeviceConfiguration? configuration;
        TypedEventHandler<SpatialAudioDeviceConfiguration, object>? handler;
        TypedEventHandler<object, DefaultAudioRenderDeviceChangedEventArgs>? defaultChanged;
        lock (_gate)
        {
            (configuration, handler, defaultChanged) = (_configuration, _configurationChanged, _defaultChanged);
            (_configuration, _defaultChanged) = (null, null);
        }

        _ = Task.Run(() =>
        {
            Unfollow(configuration, handler);
            if (defaultChanged is not null)
            {
                try
                {
                    MediaDevice.DefaultAudioRenderDeviceChanged -= defaultChanged;
                }
                catch (Exception ex)
                {
                    Log.Debug(Scope, $"the default output's handler could not be removed: {ex.Message}");
                }
            }
        });
    }
}
