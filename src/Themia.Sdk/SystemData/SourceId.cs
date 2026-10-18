using Themia.Core.SystemData;

namespace Themia.App.Services.SystemData;

/// <summary>[S-2500] A typed name for a source, so a watch says what it gets back: <c>SystemHub.Watch(Sources.Cpu, ...)</c>.</summary>
/// <typeparam name="T">The snapshot the source reads.</typeparam>
public sealed record SourceId<T>(SystemSource Source);
