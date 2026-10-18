using System.Text.Json.Serialization;
using Themia.Core.Model;

namespace Themia.Core.Storage;

/// <summary>
/// [S-6903] The JSON metadata of the built-in widgets' configs, the resolver their descriptors hand
/// the host. One context for all of them only while they are compiled into Core; a widget that moves
/// to its own project takes its config's line into that project's own context (docs/WIDGET-KIT.md
/// section 4.6). Same options as <see cref="AppStateJsonContext"/>, which a test asserts.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AirQualityConfig))]
[JsonSerializable(typeof(AlarmConfig))]
[JsonSerializable(typeof(AudioVisualizerConfig))]
[JsonSerializable(typeof(BatteryConfig))]
[JsonSerializable(typeof(BatteryDetailsConfig))]
[JsonSerializable(typeof(BrightnessConfig))]
[JsonSerializable(typeof(CalendarWidgetConfig))]
[JsonSerializable(typeof(ClipboardConfig))]
[JsonSerializable(typeof(ColorFiltersToggleConfig))]
[JsonSerializable(typeof(ContactsConfig))]
[JsonSerializable(typeof(CpuConfig))]
[JsonSerializable(typeof(CpuDetailsConfig))]
[JsonSerializable(typeof(DiskConfig))]
[JsonSerializable(typeof(DoNotDisturbToggleConfig))]
[JsonSerializable(typeof(DriveConfig))]
[JsonSerializable(typeof(EmailConfig))]
[JsonSerializable(typeof(ExchangeRatesConfig))]
[JsonSerializable(typeof(FocusSessionConfig))]
[JsonSerializable(typeof(FolderConfig))]
[JsonSerializable(typeof(GalleryConfig))]
[JsonSerializable(typeof(GameModeToggleConfig))]
[JsonSerializable(typeof(GpuConfig))]
[JsonSerializable(typeof(GpuDetailsConfig))]
[JsonSerializable(typeof(HabitTrackerConfig))]
[JsonSerializable(typeof(MemoryConfig))]
[JsonSerializable(typeof(MemoryDetailsConfig))]
[JsonSerializable(typeof(MicrophoneToggleConfig))]
[JsonSerializable(typeof(MixerConfig))]
[JsonSerializable(typeof(MonoAudioToggleConfig))]
[JsonSerializable(typeof(MusicConfig))]
[JsonSerializable(typeof(NetworkConfig))]
[JsonSerializable(typeof(NetworkDetailsConfig))]
[JsonSerializable(typeof(NextMeetingConfig))]
[JsonSerializable(typeof(NotesConfig))]
[JsonSerializable(typeof(NotificationsConfig))]
[JsonSerializable(typeof(PingConfig))]
[JsonSerializable(typeof(PingDetailsConfig))]
[JsonSerializable(typeof(RainNextHourConfig))]
[JsonSerializable(typeof(RecentFilesConfig))]
[JsonSerializable(typeof(RemovableDrivesConfig))]
[JsonSerializable(typeof(RotationLockToggleConfig))]
[JsonSerializable(typeof(RssConfig))]
[JsonSerializable(typeof(ScreenTimeAppsConfig))]
[JsonSerializable(typeof(ScreenTimeChartConfig))]
[JsonSerializable(typeof(ScreenTimeDonutConfig))]
[JsonSerializable(typeof(ShortcutsConfig))]
[JsonSerializable(typeof(SoundToggleConfig))]
[JsonSerializable(typeof(SpatialSoundToggleConfig))]
[JsonSerializable(typeof(StocksConfig))]
[JsonSerializable(typeof(TasksConfig))]
[JsonSerializable(typeof(TopProcessesConfig))]
[JsonSerializable(typeof(VideoStreamConfig))]
[JsonSerializable(typeof(VolumeConfig))]
[JsonSerializable(typeof(WeatherForecastConfig))]
[JsonSerializable(typeof(WeatherNowConfig))]
[JsonSerializable(typeof(WeekPlannerConfig))]
internal sealed partial class WidgetConfigJsonContext : JsonSerializerContext;
