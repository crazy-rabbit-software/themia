using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>
/// Per-instance widget settings. A discriminated union rather than a property bag: the
/// discriminator is written into the JSON so a config can never be silently read as the wrong
/// widget's shape.
/// <para>
/// [S-6903] The union's members are not listed here: each widget's descriptor names its config type
/// and id, and <c>StateJson</c> adds them to the union when it builds the options.
/// </para>
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
public abstract class WidgetConfig
{
    /// <summary>The kind this config belongs to. Used to detect a config/kind mismatch on load.</summary>
    [JsonIgnore]
    public abstract WidgetKind Kind { get; }

    /// <summary>Default config for a kind. Never returns null, so a widget always has settings.</summary>
    public static WidgetConfig CreateDefault(WidgetKind kind) => kind switch
    {
        WidgetKind.Clock => new ClockConfig(),
        WidgetKind.Cpu => new CpuConfig(),
        WidgetKind.Memory => new MemoryConfig(),
        WidgetKind.Disk => new DiskConfig(),
        WidgetKind.Battery => new BatteryConfig(),
        WidgetKind.Email => new EmailConfig(),
        WidgetKind.Network => new NetworkConfig(),
        WidgetKind.Ping => new PingConfig(),
        WidgetKind.Contacts => new ContactsConfig(),
        WidgetKind.Calendar => new CalendarWidgetConfig(),

        WidgetKind.Tasks => new TasksConfig(),
        WidgetKind.Notes => new NotesConfig(),
        WidgetKind.Countdown => new CountdownConfig(),
        WidgetKind.Pomodoro => new PomodoroConfig(),
        WidgetKind.Sun => new SunConfig(),
        WidgetKind.Moon => new MoonConfig(),
        WidgetKind.Folder => new FolderConfig(),
        WidgetKind.Volume => new VolumeConfig(),
        WidgetKind.Notifications => new NotificationsConfig(),
        WidgetKind.FocusSession => new FocusSessionConfig(),
        WidgetKind.Brightness => new BrightnessConfig(),
        WidgetKind.Shutdown => new ShutdownConfig(),
        WidgetKind.Restart => new RestartConfig(),
        WidgetKind.Sleep => new SleepConfig(),
        WidgetKind.Hibernate => new HibernateConfig(),
        WidgetKind.Lock => new LockConfig(),
        WidgetKind.SignOut => new SignOutConfig(),
        WidgetKind.ScreenOff => new ScreenOffConfig(),
        WidgetKind.WifiToggle => new WifiToggleConfig(),
        WidgetKind.BluetoothToggle => new BluetoothToggleConfig(),
        WidgetKind.AirplaneModeToggle => new AirplaneModeToggleConfig(),
        WidgetKind.EnergySaverToggle => new EnergySaverToggleConfig(),
        WidgetKind.NightLightToggle => new NightLightToggleConfig(),
        WidgetKind.RotationLockToggle => new RotationLockToggleConfig(),
        WidgetKind.DoNotDisturbToggle => new DoNotDisturbToggleConfig(),
        WidgetKind.SoundToggle => new SoundToggleConfig(),
        WidgetKind.MicrophoneToggle => new MicrophoneToggleConfig(),
        WidgetKind.SpatialSoundToggle => new SpatialSoundToggleConfig(),
        WidgetKind.MonoAudioToggle => new MonoAudioToggleConfig(),
        WidgetKind.ColorFiltersToggle => new ColorFiltersToggleConfig(),
        WidgetKind.GameModeToggle => new GameModeToggleConfig(),
        WidgetKind.Bluetooth => new BluetoothConfig(),
        WidgetKind.Music => new MusicConfig(),
        WidgetKind.AudioVisualizer => new AudioVisualizerConfig(),
        WidgetKind.WeatherNow => new WeatherNowConfig(),
        WidgetKind.WeatherForecast => new WeatherForecastConfig(),
        WidgetKind.Stocks => new StocksConfig(),
        WidgetKind.AirQuality => new AirQualityConfig(),
        WidgetKind.RainNextHour => new RainNextHourConfig(),
        WidgetKind.ExchangeRates => new ExchangeRatesConfig(),
        WidgetKind.Rss => new RssConfig(),
        WidgetKind.Shortcuts => ShortcutsConfig.CreateDefault(),
        WidgetKind.Clipboard => new ClipboardConfig(),
        WidgetKind.Scratchpad => new ScratchpadConfig(),
        WidgetKind.Quote => new QuoteConfig(),
        WidgetKind.Gallery => new GalleryConfig(),
        WidgetKind.HabitTracker => new HabitTrackerConfig(),
        WidgetKind.VideoStream => new VideoStreamConfig(),
        WidgetKind.Gpu => new GpuConfig(),
        WidgetKind.Mixer => new MixerConfig(),
        WidgetKind.ScreenTimeChart => new ScreenTimeChartConfig(),
        WidgetKind.ScreenTimeDonut => new ScreenTimeDonutConfig(),
        WidgetKind.ScreenTimeApps => new ScreenTimeAppsConfig(),
        WidgetKind.CpuDetails => new CpuDetailsConfig(),
        WidgetKind.MemoryDetails => new MemoryDetailsConfig(),
        WidgetKind.TopProcesses => new TopProcessesConfig(),
        WidgetKind.NetworkDetails => new NetworkDetailsConfig(),
        WidgetKind.BatteryDetails => new BatteryDetailsConfig(),
        WidgetKind.PingDetails => new PingDetailsConfig(),
        WidgetKind.GpuDetails => new GpuDetailsConfig(),
        WidgetKind.Stopwatch => new StopwatchConfig(),
        WidgetKind.Alarm => new AlarmConfig(),
        WidgetKind.RemovableDrives => new RemovableDrivesConfig(),
        WidgetKind.Drive => new DriveConfig(),
        WidgetKind.YearProgress => new YearProgressConfig(),
        WidgetKind.NextMeeting => new NextMeetingConfig(),
        WidgetKind.RecentFiles => new RecentFilesConfig(),
        WidgetKind.RecycleBin => new RecycleBinConfig(),
        WidgetKind.WeekPlanner => new WeekPlannerConfig(),
        _ => new ClockConfig(),
    };

    public abstract WidgetConfig Clone();

    /// <summary>
    /// Bring a config read from disk back inside its own invariants — clamp ranges, drop values a
    /// hand edit made unusable — and say what had to change. Run once at load by
    /// <c>AppStateStore.Validate</c>, so the repairs are logged with the rest. Most configs have
    /// nothing to repair.
    /// </summary>
    public virtual IReadOnlyList<string> Repair() => [];
}
