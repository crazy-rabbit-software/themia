using System.Text.Json;
using System.Text.Json.Serialization;
using Themia.Core.Model;

namespace Themia.Core.Storage;

/// <summary>
/// Source-generated serialization metadata. Required for Native AOT: reflection-based
/// <see cref="JsonSerializer"/> overloads are trimmed away, so every persisted type is declared here.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AppState))]
[JsonSerializable(typeof(ViewState))]
[JsonSerializable(typeof(WidgetInstance))]
[JsonSerializable(typeof(WidgetConfig))]
[JsonSerializable(typeof(ClockConfig))]
[JsonSerializable(typeof(CpuConfig))]
[JsonSerializable(typeof(MemoryConfig))]
[JsonSerializable(typeof(DiskConfig))]
[JsonSerializable(typeof(BatteryConfig))]
[JsonSerializable(typeof(EmailConfig))]
[JsonSerializable(typeof(NetworkConfig))]
[JsonSerializable(typeof(PingConfig))]
[JsonSerializable(typeof(ContactsConfig))]
[JsonSerializable(typeof(CalendarConfig))]
[JsonSerializable(typeof(TasksConfig))]
[JsonSerializable(typeof(NotesConfig))]
[JsonSerializable(typeof(CountdownConfig))]
[JsonSerializable(typeof(PomodoroConfig))]
[JsonSerializable(typeof(SunMoonConfig))]
[JsonSerializable(typeof(FolderConfig))]
[JsonSerializable(typeof(VolumeConfig))]
[JsonSerializable(typeof(ShutdownConfig))]
[JsonSerializable(typeof(RestartConfig))]
[JsonSerializable(typeof(SleepConfig))]
[JsonSerializable(typeof(HibernateConfig))]
[JsonSerializable(typeof(LockConfig))]
[JsonSerializable(typeof(SignOutConfig))]
[JsonSerializable(typeof(ScreenOffConfig))]
[JsonSerializable(typeof(BluetoothConfig))]
[JsonSerializable(typeof(MusicConfig))]
[JsonSerializable(typeof(WeatherNowConfig))]
[JsonSerializable(typeof(WeatherForecastConfig))]
[JsonSerializable(typeof(StocksConfig))]
[JsonSerializable(typeof(RssConfig))]
[JsonSerializable(typeof(ShortcutsConfig))]
[JsonSerializable(typeof(ClipboardConfig))]
[JsonSerializable(typeof(ScratchpadConfig))]
[JsonSerializable(typeof(QuoteConfig))]
[JsonSerializable(typeof(GalleryConfig))]
[JsonSerializable(typeof(HabitTrackerConfig))]
[JsonSerializable(typeof(VideoStreamConfig))]
[JsonSerializable(typeof(GpuConfig))]
[JsonSerializable(typeof(MixerConfig))]
[JsonSerializable(typeof(ScreenTimeConfig))]
[JsonSerializable(typeof(CpuDetailsConfig))]
[JsonSerializable(typeof(MemoryDetailsConfig))]
[JsonSerializable(typeof(TopProcessesConfig))]
[JsonSerializable(typeof(NetworkDetailsConfig))]
[JsonSerializable(typeof(DiskSpaceConfig))]
[JsonSerializable(typeof(BatteryDetailsConfig))]
[JsonSerializable(typeof(PingDetailsConfig))]
[JsonSerializable(typeof(GpuDetailsConfig))]
[JsonSerializable(typeof(DesignState))]
[JsonSerializable(typeof(AppPreferences))]
internal sealed partial class AppStateJsonContext : JsonSerializerContext;
