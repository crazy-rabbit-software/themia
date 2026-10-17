using System.Text.Json;
using System.Text.Json.Serialization;
using Themia.Core.Model;

namespace Themia.Core.Storage;

/// <summary>
/// Source-generated serialization metadata of the host's own state. The widgets' configs are not
/// here: each module brings its own (<see cref="WidgetConfigJsonContext"/> for the built-in ones)
/// and <see cref="StateJson"/> combines them ([S-6903]).
/// <para>
/// Source-generated serialization metadata. Required for Native AOT: reflection-based
/// <see cref="JsonSerializer"/> overloads are trimmed away, so every persisted type is declared here.
/// </para>
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AppState))]
[JsonSerializable(typeof(ViewState))]
[JsonSerializable(typeof(WidgetInstance))]
[JsonSerializable(typeof(WidgetConfig))]
[JsonSerializable(typeof(DesignState))]
[JsonSerializable(typeof(AppPreferences))]
internal sealed partial class AppStateJsonContext : JsonSerializerContext;
