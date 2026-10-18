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
[JsonSerializable(typeof(AlarmConfig))]
[JsonSerializable(typeof(AudioVisualizerConfig))]
[JsonSerializable(typeof(BrightnessConfig))]
[JsonSerializable(typeof(CalendarWidgetConfig))]
[JsonSerializable(typeof(ClipboardConfig))]
[JsonSerializable(typeof(ContactsConfig))]
[JsonSerializable(typeof(DriveConfig))]
[JsonSerializable(typeof(EmailConfig))]
[JsonSerializable(typeof(FocusSessionConfig))]
[JsonSerializable(typeof(FolderConfig))]
[JsonSerializable(typeof(GalleryConfig))]
[JsonSerializable(typeof(GpuDetailsConfig))]
[JsonSerializable(typeof(HabitTrackerConfig))]
[JsonSerializable(typeof(MixerConfig))]
[JsonSerializable(typeof(MusicConfig))]
[JsonSerializable(typeof(NextMeetingConfig))]
[JsonSerializable(typeof(NotesConfig))]
[JsonSerializable(typeof(NotificationsConfig))]
[JsonSerializable(typeof(PingDetailsConfig))]
[JsonSerializable(typeof(RecentFilesConfig))]
[JsonSerializable(typeof(RemovableDrivesConfig))]
[JsonSerializable(typeof(RssConfig))]
[JsonSerializable(typeof(ScreenTimeAppsConfig))]
[JsonSerializable(typeof(ScreenTimeChartConfig))]
[JsonSerializable(typeof(ScreenTimeDonutConfig))]
[JsonSerializable(typeof(ShortcutsConfig))]
[JsonSerializable(typeof(TasksConfig))]
[JsonSerializable(typeof(VideoStreamConfig))]
[JsonSerializable(typeof(VolumeConfig))]
[JsonSerializable(typeof(WeekPlannerConfig))]
internal sealed partial class WidgetConfigJsonContext : JsonSerializerContext;
