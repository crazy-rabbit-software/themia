using System.Text.Json.Serialization;

namespace Themia.Core.Sync;

/// <summary>
/// [S-2660] The web widgets' shelf: the feed and quote cache ([S-1300]) or the weather cache ([S-729]),
/// as the document those stores already validate, sealed like every other shelf. The inner document
/// keeps its own schema version and entry checks; this envelope only says whose it is.
/// </summary>
public sealed class WebShelf : ISyncShelf
{
    public const int Version = 1;

    /// <summary>The feed and quote cache's owner name.</summary>
    public const string NetworkOwner = "web:network-cache";

    /// <summary>The weather cache's owner name.</summary>
    public const string WeatherOwner = "web:weather-cache";

    /// <summary>[S-5161] The air, rain and exchange-rate cache's owner name.</summary>
    public const string OnlineOwner = "web:online-cache";

    public int SchemaVersion { get; set; } = Version;

    public string Owner { get; set; } = string.Empty;

    public string Json { get; set; } = string.Empty;
}

/// <summary>Source-generated metadata for every shelf (Native AOT: no reflection JSON).</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(MailShelf))]
[JsonSerializable(typeof(CalendarShelf))]
[JsonSerializable(typeof(ContactsShelf))]
[JsonSerializable(typeof(TasksShelf))]
[JsonSerializable(typeof(WebShelf))]
public sealed partial class SyncJsonContext : JsonSerializerContext;
