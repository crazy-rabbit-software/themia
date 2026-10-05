using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>
/// Per-instance widget settings. A discriminated union rather than a property bag: the
/// discriminator is written into the JSON so a config can never be silently read as the wrong
/// widget's shape.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(ClockConfig), typeDiscriminator: "clock")]
[JsonDerivedType(typeof(CpuConfig), typeDiscriminator: "cpu")]
[JsonDerivedType(typeof(MemoryConfig), typeDiscriminator: "memory")]
[JsonDerivedType(typeof(DiskConfig), typeDiscriminator: "disk")]
[JsonDerivedType(typeof(BatteryConfig), typeDiscriminator: "battery")]
[JsonDerivedType(typeof(EmailConfig), typeDiscriminator: "email")]
[JsonDerivedType(typeof(NetworkConfig), typeDiscriminator: "network")]
[JsonDerivedType(typeof(PingConfig), typeDiscriminator: "ping")]
[JsonDerivedType(typeof(ContactsConfig), typeDiscriminator: "contacts")]
[JsonDerivedType(typeof(CalendarConfig), typeDiscriminator: "calendar")]
[JsonDerivedType(typeof(TasksConfig), typeDiscriminator: "tasks")]
[JsonDerivedType(typeof(NotesConfig), typeDiscriminator: "notes")]
[JsonDerivedType(typeof(CountdownConfig), typeDiscriminator: "countdown")]
[JsonDerivedType(typeof(PomodoroConfig), typeDiscriminator: "pomodoro")]
[JsonDerivedType(typeof(SunMoonConfig), typeDiscriminator: "sunMoon")]
[JsonDerivedType(typeof(FolderConfig), typeDiscriminator: "folder")]
[JsonDerivedType(typeof(VolumeConfig), typeDiscriminator: "volume")]
[JsonDerivedType(typeof(ShutdownConfig), typeDiscriminator: "shutdown")]
[JsonDerivedType(typeof(RestartConfig), typeDiscriminator: "restart")]
[JsonDerivedType(typeof(SleepConfig), typeDiscriminator: "sleep")]
[JsonDerivedType(typeof(HibernateConfig), typeDiscriminator: "hibernate")]
[JsonDerivedType(typeof(LockConfig), typeDiscriminator: "lock")]
[JsonDerivedType(typeof(SignOutConfig), typeDiscriminator: "signOut")]
[JsonDerivedType(typeof(ScreenOffConfig), typeDiscriminator: "screenOff")]
[JsonDerivedType(typeof(BluetoothConfig), typeDiscriminator: "bluetooth")]
[JsonDerivedType(typeof(MusicConfig), typeDiscriminator: "music")]
[JsonDerivedType(typeof(WeatherNowConfig), typeDiscriminator: "weatherNow")]
[JsonDerivedType(typeof(WeatherForecastConfig), typeDiscriminator: "weatherForecast")]
[JsonDerivedType(typeof(StocksConfig), typeDiscriminator: "stocks")]
[JsonDerivedType(typeof(RssConfig), typeDiscriminator: "rss")]
[JsonDerivedType(typeof(ShortcutsConfig), typeDiscriminator: "shortcuts")]
[JsonDerivedType(typeof(ClipboardConfig), typeDiscriminator: "clipboard")]
[JsonDerivedType(typeof(ScratchpadConfig), typeDiscriminator: "scratchpad")]
[JsonDerivedType(typeof(QuoteConfig), typeDiscriminator: "quote")]
[JsonDerivedType(typeof(GalleryConfig), typeDiscriminator: "gallery")]
[JsonDerivedType(typeof(HabitTrackerConfig), typeDiscriminator: "habitTracker")]
[JsonDerivedType(typeof(VideoStreamConfig), typeDiscriminator: "videoStream")]
[JsonDerivedType(typeof(GpuConfig), typeDiscriminator: "gpu")]
[JsonDerivedType(typeof(MixerConfig), typeDiscriminator: "mixer")]
[JsonDerivedType(typeof(ScreenTimeConfig), typeDiscriminator: "screenTime")]
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
        WidgetKind.Calendar => new CalendarConfig(),
        WidgetKind.Tasks => new TasksConfig(),
        WidgetKind.Notes => new NotesConfig(),
        WidgetKind.Countdown => new CountdownConfig(),
        WidgetKind.Pomodoro => new PomodoroConfig(),
        WidgetKind.SunMoon => new SunMoonConfig(),
        WidgetKind.Folder => new FolderConfig(),
        WidgetKind.Volume => new VolumeConfig(),
        WidgetKind.Shutdown => new ShutdownConfig(),
        WidgetKind.Restart => new RestartConfig(),
        WidgetKind.Sleep => new SleepConfig(),
        WidgetKind.Hibernate => new HibernateConfig(),
        WidgetKind.Lock => new LockConfig(),
        WidgetKind.SignOut => new SignOutConfig(),
        WidgetKind.ScreenOff => new ScreenOffConfig(),
        WidgetKind.Bluetooth => new BluetoothConfig(),
        WidgetKind.Music => new MusicConfig(),
        WidgetKind.WeatherNow => new WeatherNowConfig(),
        WidgetKind.WeatherForecast => new WeatherForecastConfig(),
        WidgetKind.Stocks => new StocksConfig(),
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
        WidgetKind.ScreenTime => new ScreenTimeConfig(),
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
