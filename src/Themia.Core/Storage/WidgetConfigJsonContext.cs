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
[JsonSerializable(typeof(CalendarWidgetConfig))]
[JsonSerializable(typeof(ContactsConfig))]
[JsonSerializable(typeof(EmailConfig))]
[JsonSerializable(typeof(TasksConfig))]
[JsonSerializable(typeof(WeekPlannerConfig))]
internal sealed partial class WidgetConfigJsonContext : JsonSerializerContext;
